using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using ZauberCMS.Core.Email.Interfaces;
using ZauberCMS.Core.Email.Parameters;
using ZauberCMS.Core.Extensions;
using ZauberCMS.Core.Localization;
using ZauberCMS.Core.Membership.Models;
using ZauberCMS.Core.Providers;
using ZauberCMS.Core.Settings;

namespace ZauberCMS.Core.Email.Services;

public class EmailService(
    IServiceScopeFactory serviceScopeFactory,
    IHttpContextAccessor httpContextAccessor,
    ProviderService providerService,
    IStringLocalizer<ZauberText> text)
    : IEmailService
{
    /// <summary>
    /// Sends an email confirmation (or change email) message to a user using the configured provider.
    /// </summary>
    /// <param name="parameters">Target user, optional new email and return url.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task SendEmailConfirmationAsync(SendEmailConfirmationParameters parameters, CancellationToken cancellationToken = default)
    {
        // A UserManager per call, not the circuit's, which lives as long as the circuit (ZA-086)
        using var scope = serviceScopeFactory.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var userId = await userManager.GetUserIdAsync(parameters.User!);

        string code;
        string email;

        // Is this a change of email or a new signup
        var isChange = "false";
        if (parameters.NewEmailAddress.IsNullOrWhiteSpace())
        {
            code = await userManager.GenerateEmailConfirmationTokenAsync(parameters.User!);
            email = parameters.User!.Email!;
        }
        else
        {
            isChange = "true";
            code = await userManager.GenerateChangeEmailTokenAsync(parameters.User!, parameters.NewEmailAddress);
            email = parameters.NewEmailAddress;
        }

        code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));

        var callbackUrl = $"{ConfirmEmailAddress(scope.ServiceProvider)}?userId={userId}&code={code}&change={isChange}&returnUrl={Uri.EscapeDataString(parameters.ReturnUrl ?? string.Empty)}";
        if (isChange == "true")
        {
            // The confirm page needs the new address to apply the change; the code is only valid for it.
            callbackUrl += $"&email={Uri.EscapeDataString(email)}";
        }
        
        var link = HtmlEncoder.Default.Encode(callbackUrl);
        if (isChange == "true")
        {
            // Sent to the new address only: the account moves there when the link is opened (Roadmap D72).
            // In the caller's language; the link's opening and closing tags are the placeholders.
            await providerService.GetRequiredEmailProvider().SendEmailWithTemplateAsync(email, text["email_changeEmailSubject"],
            [
                EmailHtml.Paragraph(text["email_changeEmailBody"], $"<a href='{link}'>", "</a>"),
                EmailHtml.Paragraph(text["email_changeEmailIgnore"])
            ]);
            return;
        }

        var paragraphs = new List<string> { EmailHtml.Paragraph(text["email_confirmAccountBody"], $"<a href='{link}'>", "</a>") };

        await providerService.GetRequiredEmailProvider().SendEmailWithTemplateAsync(email!, text["email_confirmAccountSubject"], paragraphs);
    }

    /// <summary>
    /// The confirmation page's address. Inside an admin circuit there may be no request to take the host from
    /// (Long Polling, Azure SignalR): <c>Zauber:SiteUrl</c> is used then, and without it the send fails naming
    /// the setting.
    /// </summary>
    private string ConfirmEmailAddress(IServiceProvider services)
    {
        if (httpContextAccessor.HttpContext != null)
        {
            return httpContextAccessor.ToAbsoluteUrl(Urls.Account.ConfirmEmail);
        }

        var siteUrl = services.GetRequiredService<IOptions<ZauberSettings>>().Value.SiteUrl;
        if (Uri.TryCreate(siteUrl, UriKind.Absolute, out var site) &&
            (site.Scheme == Uri.UriSchemeHttp || site.Scheme == Uri.UriSchemeHttps))
        {
            return new Uri(site, Urls.Account.ConfirmEmail).ToString();
        }

        throw new InvalidOperationException(
            "The email link needs the site's address: set Zauber:SiteUrl (there is no request to take it from).");
    }
}
