using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using ZauberCMS.Core.Email.Interfaces;
using ZauberCMS.Core.Email.Parameters;
using ZauberCMS.Core.Extensions;
using ZauberCMS.Core.Membership.Models;
using ZauberCMS.Core.Providers;

namespace ZauberCMS.Core.Email.Services;

public class EmailService(
    IServiceScopeFactory serviceScopeFactory,
    IHttpContextAccessor httpContextAccessor,
    ProviderService providerService)
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

        var callbackUrl = $"{httpContextAccessor.ToAbsoluteUrl(Urls.Account.ConfirmEmail)}?userId={userId}&code={code}&change={isChange}&returnUrl={Uri.EscapeDataString(parameters.ReturnUrl ?? string.Empty)}";
        if (isChange == "true")
        {
            // The confirm page needs the new address to apply the change; the code is only valid for it.
            callbackUrl += $"&email={Uri.EscapeDataString(email)}";
        }
        
        var paragraphs = new List<string> { $"Please confirm your account by <a href='{HtmlEncoder.Default.Encode(callbackUrl)}'>clicking here</a>." };

        await providerService.EmailProvider!.SendEmailWithTemplateAsync(email!, "Confirm your email", paragraphs);
    }
}
