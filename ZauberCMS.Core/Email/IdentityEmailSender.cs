using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Localization;
using ZauberCMS.Core.Localization;
using ZauberCMS.Core.Membership.Models;
using ZauberCMS.Core.Providers;

namespace ZauberCMS.Core.Email;

public class IdentityEmailSender(ProviderService providerService, IStringLocalizer<ZauberText> text) : IEmailSender<User>
{
    // The sentences are encoded text and the links' tags their placeholders (EmailHtml). The links and the code
    // come HTML-encoded already (IEmailSender: "Do not double encode this")
    public Task SendConfirmationLinkAsync(User user, string email, string confirmationLink)=>  
        providerService.GetRequiredEmailProvider().SendEmailAsync(email, text["email_confirmAccountSubject"],
            EmailHtml.Paragraph(text["email_confirmAccountBody"], $"<a href='{confirmationLink}'>", "</a>"));

    public Task SendPasswordResetLinkAsync(User user, string email, string resetLink) =>
        providerService.GetRequiredEmailProvider().SendEmailAsync(email, text["email_resetYourPasswordSubject"],
            EmailHtml.Paragraph(text["email_resetPasswordBody"], $"<a href='{resetLink}'>", "</a>"));

    public Task SendPasswordResetCodeAsync(User user, string email, string resetCode) =>
        providerService.GetRequiredEmailProvider().SendEmailAsync(email, text["email_resetYourPasswordSubject"],
            EmailHtml.Paragraph(text["email_resetPasswordCode"], resetCode));
}
