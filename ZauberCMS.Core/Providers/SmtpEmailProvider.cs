using System.Text;
using MailKit.Security;
using MediatR;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ZauberCMS.Core.Plugins.Interfaces;
using ZauberCMS.Core.Settings;
using MimeKit;
using MimeKit.Text;
using SmtpClient = MailKit.Net.Smtp.SmtpClient;

namespace ZauberCMS.Core.Providers;

public class SmtpEmailProvider(
    IOptions<ZauberSettings> settings,
    IWebHostEnvironment env,
    IHttpContextAccessor httpContextAccessor,
    IServiceProvider serviceProvider)
    : IEmailProvider
{
        private readonly ZauberSettings _gabSettings = settings.Value;
        private readonly IHttpContextAccessor _httpContextAccessor = httpContextAccessor;

        public async Task SendEmailWithTemplateAsync(string toEmail, string subject, List<string> paragraphs)
        {
            using var scope = serviceProvider.CreateScope();
            var mediatr = scope.ServiceProvider.GetRequiredService<IMediator>();
            
            // Get the default email template and the logo
            //string webRootPath = _env.WebRootPath;
            var emailTemplateUrl = $"{env.WebRootPath}{Path.DirectorySeparatorChar}templates{Path.DirectorySeparatorChar}email{Path.DirectorySeparatorChar}{Constants.Assets.DefaultEmailTemplate}";
            //var logoUrl = _httpContextAccessor.ToAbsoluteUrl((settings.Logo != null ? settings.Logo.Url : Constants.DefaultLogo) ?? string.Empty);

            // Get template html
            using var sourceReader = File.OpenText(emailTemplateUrl);
            var emailTemplate = await sourceReader.ReadToEndAsync();

            // send email
            using var smtp = new SmtpClient();

            var emailMessage = new MimeMessage();
            emailMessage.From.Add(MailboxAddress.Parse(_gabSettings.Email.SenderEmail));
            emailMessage.To.Add(MailboxAddress.Parse(toEmail));
            emailMessage.Subject = subject;
            emailMessage.Body = new TextPart(TextFormat.Html)
            {
                Text = HtmlBody(paragraphs, emailTemplate, string.Empty)
            };

            await smtp.ConnectAsync(_gabSettings.Email.Smtp.Host, _gabSettings.Email.Smtp.Port, SecureSocketOptions.StartTls);
            await smtp.AuthenticateAsync(_gabSettings.Email.Smtp.Username, _gabSettings.Email.Smtp.Password);
            await smtp.SendAsync(emailMessage);
            await smtp.DisconnectAsync(true);
        }

        public async Task SendEmailAsync(string toEmail, string subject, string message)
        {
            // send email
            using var smtp = new SmtpClient();

            var emailMessage = new MimeMessage();
            emailMessage.From.Add(MailboxAddress.Parse(_gabSettings.Email.SenderEmail));
            emailMessage.To.Add(MailboxAddress.Parse(toEmail));
            emailMessage.Subject = subject;
            emailMessage.Body = new TextPart(TextFormat.Html)
            {
                Text = message
            };

            await smtp.ConnectAsync(_gabSettings.Email.Smtp.Host, _gabSettings.Email.Smtp.Port, SecureSocketOptions.StartTls);
            await smtp.AuthenticateAsync(_gabSettings.Email.Smtp.Username, _gabSettings.Email.Smtp.Password);
            await smtp.SendAsync(emailMessage);
            await smtp.DisconnectAsync(true);
        }

        private static string FormatParagraphs(string text)
        {
            return $"<p style=\"Margin:0;-webkit-text-size-adjust:none;-ms-text-size-adjust:none;mso-line-height-rule:exactly;font-size:17px;font-family:helvetica, 'helvetica neue', arial, verdana, sans-serif;line-height:28px;color:#333333\">{text}</p>";
        }

        private static string HtmlBody(List<string> paragraphs, string emailTemplate, string logoUrl)
        {
            // Replace logo
            emailTemplate = emailTemplate.Replace("##LOGO##", logoUrl);

            var sb = new StringBuilder();
            foreach (var para in paragraphs)
            {
                sb.AppendLine(FormatParagraphs(para));
            }

            // Replace content
            emailTemplate = emailTemplate.Replace("##CONTENT##", sb.ToString());

            return emailTemplate;
        }
    }