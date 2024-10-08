using System.Text;
using MailKit.Security;
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
    IServiceProvider serviceProvider)
    : IEmailProvider
{
        private readonly ZauberSettings _gabSettings = settings.Value;
        
        public async Task SendEmailWithTemplateAsync(string toEmail, string subject, List<string> paragraphs)
        {
            using var scope = serviceProvider.CreateScope();

            // send email
            using var smtp = new SmtpClient();

            var emailMessage = new MimeMessage();
            emailMessage.From.Add(MailboxAddress.Parse(_gabSettings.Email.SenderEmail));
            emailMessage.To.Add(MailboxAddress.Parse(toEmail));
            emailMessage.Subject = subject;
            emailMessage.Body = new TextPart(TextFormat.Html)
            {
                Text = HtmlBody(paragraphs, Template, string.Empty)
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

        public const string Template = """
                                  
                                          <!DOCTYPE html PUBLIC "-//W3C//DTD XHTML 1.0 Transitional//EN"
                                          "http://www.w3.org/TR/xhtml1/DTD/xhtml1-transitional.dtd">
                                  <html xmlns="http://www.w3.org/1999/xhtml" xmlns:o="urn:schemas-microsoft-com:office:office"
                                        style="width:100%;font-family:helvetica, 'helvetica neue', arial, verdana, sans-serif;-webkit-text-size-adjust:100%;-ms-text-size-adjust:100%;padding:0;Margin:0;background-color:#F9FAFB;">
                                  <head>
                                      <meta charset="UTF-8">
                                      <meta content="width=device-width, initial-scale=1" name="viewport">
                                      <meta name="x-apple-disable-message-reformatting">
                                      <meta http-equiv="X-UA-Compatible" content="IE=edge">
                                      <meta content="telephone=no" name="format-detection">
                                      <title>Email</title>
                                      <!--[if (mso 16)]>
                                      <style type="text/css">
                                          a {
                                              text-decoration: none;
                                          }
                                      </style>
                                      <![endif]-->
                                      <!--[if gte mso 9]>
                                      <style>sup {
                                          font-size: 100% !important;
                                      }</style><![endif]-->
                                      <!--[if gte mso 9]>
                                      <xml>
                                          <o:OfficeDocumentSettings>
                                              <o:AllowPNG></o:AllowPNG>
                                              <o:PixelsPerInch>96</o:PixelsPerInch>
                                          </o:OfficeDocumentSettings>
                                      </xml>
                                      <![endif]-->
                                      <style type="text/css">
                                          #outlook a {
                                              padding: 0;
                                          }
                                  
                                          .ExternalClass {
                                              width: 100%;
                                          }
                                  
                                          .ExternalClass,
                                          .ExternalClass p,
                                          .ExternalClass span,
                                          .ExternalClass font,
                                          .ExternalClass td,
                                          .ExternalClass div {
                                              line-height: 100%;
                                          }
                                  
                                          .es-button {
                                              mso-style-priority: 100 !important;
                                              text-decoration: none !important;
                                          }
                                  
                                          a[x-apple-data-detectors] {
                                              color: inherit !important;
                                              text-decoration: none !important;
                                              font-size: inherit !important;
                                              font-family: inherit !important;
                                              font-weight: inherit !important;
                                              line-height: inherit !important;
                                          }
                                  
                                          .es-desk-hidden {
                                              display: none;
                                              float: left;
                                              overflow: hidden;
                                              width: 0;
                                              max-height: 0;
                                              line-height: 0;
                                              mso-hide: all;
                                          }
                                  
                                          @media only screen and (max-width: 600px) {
                                              p, ul li, ol li, a {
                                                  font-size: 16px !important;
                                                  line-height: 150% !important
                                              }
                                  
                                              h1 {
                                                  font-size: 30px !important;
                                                  text-align: center;
                                                  line-height: 120%
                                              }
                                  
                                              h2 {
                                                  font-size: 26px !important;
                                                  text-align: center;
                                                  line-height: 120%
                                              }
                                  
                                              h3 {
                                                  font-size: 20px !important;
                                                  text-align: center;
                                                  line-height: 120%
                                              }
                                  
                                              h1 a {
                                                  font-size: 30px !important
                                              }
                                  
                                              h2 a {
                                                  font-size: 26px !important
                                              }
                                  
                                              h3 a {
                                                  font-size: 20px !important
                                              }
                                  
                                              .es-menu td a {
                                                  font-size: 16px !important
                                              }
                                  
                                              .es-header-body p, .es-header-body ul li, .es-header-body ol li, .es-header-body a {
                                                  font-size: 16px !important
                                              }
                                  
                                              .es-footer-body p, .es-footer-body ul li, .es-footer-body ol li, .es-footer-body a {
                                                  font-size: 16px !important
                                              }
                                  
                                              .es-infoblock p, .es-infoblock ul li, .es-infoblock ol li, .es-infoblock a {
                                                  font-size: 12px !important
                                              }
                                  
                                              *[class="gmail-fix"] {
                                                  display: none !important
                                              }
                                  
                                              .es-m-txt-c, .es-m-txt-c h1, .es-m-txt-c h2, .es-m-txt-c h3 {
                                                  text-align: center !important
                                              }
                                  
                                              .es-m-txt-r, .es-m-txt-r h1, .es-m-txt-r h2, .es-m-txt-r h3 {
                                                  text-align: right !important
                                              }
                                  
                                              .es-m-txt-l, .es-m-txt-l h1, .es-m-txt-l h2, .es-m-txt-l h3 {
                                                  text-align: left !important
                                              }
                                  
                                              .es-m-txt-r img, .es-m-txt-c img, .es-m-txt-l img {
                                                  display: inline !important
                                              }
                                  
                                              .es-button-border {
                                                  display: block !important
                                              }
                                  
                                              .es-btn-fw {
                                                  border-width: 10px 0px !important;
                                                  text-align: center !important
                                              }
                                  
                                              .es-adaptive table, .es-btn-fw, .es-btn-fw-brdr, .es-left, .es-right {
                                                  width: 100% !important
                                              }
                                  
                                              .es-content table, .es-header table, .es-footer table, .es-content, .es-footer, .es-header {
                                                  width: 100% !important;
                                                  max-width: 600px !important
                                              }
                                  
                                              .es-adapt-td {
                                                  display: block !important;
                                                  width: 100% !important
                                              }
                                  
                                              .adapt-img {
                                                  width: 100% !important;
                                                  height: auto !important
                                              }
                                  
                                              .es-m-p0 {
                                                  padding: 0px !important
                                              }
                                  
                                              .es-m-p0r {
                                                  padding-right: 0px !important
                                              }
                                  
                                              .es-m-p0l {
                                                  padding-left: 0px !important
                                              }
                                  
                                              .es-m-p0t {
                                                  padding-top: 0px !important
                                              }
                                  
                                              .es-m-p0b {
                                                  padding-bottom: 0 !important
                                              }
                                  
                                              .es-m-p20b {
                                                  padding-bottom: 20px !important
                                              }
                                  
                                              .es-mobile-hidden, .es-hidden {
                                                  display: none !important
                                              }
                                  
                                              tr.es-desk-hidden, td.es-desk-hidden, table.es-desk-hidden {
                                                  width: auto !important;
                                                  overflow: visible !important;
                                                  float: none !important;
                                                  max-height: inherit !important;
                                                  line-height: inherit !important
                                              }
                                  
                                              tr.es-desk-hidden {
                                                  display: table-row !important
                                              }
                                  
                                              table.es-desk-hidden {
                                                  display: table !important
                                              }
                                  
                                              td.es-desk-menu-hidden {
                                                  display: table-cell !important
                                              }
                                  
                                              .es-menu td {
                                                  width: 1% !important
                                              }
                                  
                                              table.es-table-not-adapt, .esd-block-html table {
                                                  width: auto !important
                                              }
                                  
                                              table.es-social {
                                                  display: inline-block !important
                                              }
                                  
                                              table.es-social td {
                                                  display: inline-block !important
                                              }
                                  
                                              a.es-button, button.es-button {
                                                  font-size: 20px !important;
                                                  display: block !important;
                                                  border-width: 10px 0px 10px 0px !important
                                              }
                                          }
                                      </style>
                                  </head>
                                  <body style="width:100%;font-family:helvetica, 'helvetica neue', arial, verdana, sans-serif;-webkit-text-size-adjust:100%;-ms-text-size-adjust:100%;padding-top:20px;padding-bottom:20px;padding-left:0;padding-right:0;Margin:0"
                                        style="background-color:#F9FAFB">
                                  <div class="es-wrapper-color" style="background-color:#F9FAFB">
                                      <!--[if gte mso 9]>
                                      <v:background xmlns:v="urn:schemas-microsoft-com:vml" fill="t">
                                          <v:fill type="tile" color="#F9FAFB" origin="0.5, 0" position="0.5,0"></v:fill>
                                      </v:background>
                                      <![endif]-->
                                      <table class="es-wrapper" width="100%" cellspacing="0" cellpadding="0"
                                             style="mso-table-lspace:0pt;mso-table-rspace:0pt;border-collapse:collapse;border-spacing:0px;padding:0;Margin:0;width:100%;height:100%;background-repeat:repeat;background-position:center top">
                                          <tr style="border-collapse:collapse">
                                              <td valign="top" style="padding:0;Margin:0">
                                                  <table class="es-content" cellspacing="0" cellpadding="0" align="center"
                                                         style="mso-table-lspace:0pt;mso-table-rspace:0pt;border-collapse:collapse;border-spacing:0px;table-layout:fixed !important;width:100%">
                                                      <tr style="border-collapse:collapse">
                                                          <td align="center" style="padding:0;Margin:0">
                                                              <table class="es-content-body" cellspacing="0" cellpadding="0" bgcolor="#ffffff"
                                                                     align="center"
                                                                     style="mso-table-lspace:0pt;mso-table-rspace:0pt;border-collapse:collapse;border-spacing:0px;background-color:#FFFFFF;border-top:1px solid #EEECEC;border-right:1px solid #EEECEC;border-left:1px solid #EEECEC;width:600px;border-bottom:1px solid #EEECEC">
                                                                  <tr style="border-collapse:collapse">
                                                                      <td align="left" style="padding:20px;Margin:0">
                                                                          <table width="100%" cellspacing="0" cellpadding="0"
                                                                                 style="mso-table-lspace:0pt;mso-table-rspace:0pt;border-collapse:collapse;border-spacing:0px">
                                                                              <tr style="border-collapse:collapse">
                                                                                  <td valign="top" align="center" style="padding:0;Margin:0;width:558px">
                                                                                      <table width="100%" cellspacing="0" cellpadding="0"
                                                                                             role="presentation"
                                                                                             style="mso-table-lspace:0pt;mso-table-rspace:0pt;border-collapse:collapse;border-spacing:0px">
                                                                                          <tr style="border-collapse:collapse">
                                                                                              <td align="center"
                                                                                                  style="padding: 0; Margin: 0; padding-bottom: 10px; font-size: 0px">
                                                                                                  <img class="adapt-img" src="##LOGO##" alt=alt
                                                                                                       style="display:block;border:0;outline:none;text-decoration:none;-ms-interpolation-mode:bicubic"
                                                                                                       width="60"/>
                                                                                              </td>
                                                                                          </tr>
                                                                                          <tr style="border-collapse:collapse">
                                                                                              <td align="center" style="padding:0;Margin:0">
                                                                                                  ##CONTENT##
                                                                                              </td>
                                                                                          </tr>
                                                                                      </table>
                                                                                  </td>
                                                                              </tr>
                                                                          </table>
                                                                      </td>
                                                                  </tr>
                                                              </table>
                                                          </td>
                                                      </tr>
                                                  </table>
                                              </td>
                                          </tr>
                                      </table>
                                  </div>
                                  </body>
                                  </html>

                                  """;
    }