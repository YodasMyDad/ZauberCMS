using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using System.Text;
using System.Text.Encodings.Web;
using ZauberCMS.Core.Email.Interfaces;
using ZauberCMS.Core.Email.Parameters;
using ZauberCMS.Core.Extensions;
using ZauberCMS.Core.Membership.Models;
using ZauberCMS.Core.Providers;

namespace ZauberCMS.Core.Email.Services;

public class EmailService(
    IServiceProvider serviceProvider,
    ProviderService providerService,
    IHttpContextAccessor httpContextAccessor) : IEmailService
{
    public async Task SendEmailConfirmationAsync(SendEmailConfirmationParameters parameters, CancellationToken cancellationToken = default)
    {
        using var scope = serviceProvider.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();

        var userId = await userManager.GetUserIdAsync(parameters.User!);

        string code;
        string email;

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
            email = parameters.NewEmailAddress!;
        }

        code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));

        var callbackUrl = $"{httpContextAccessor.ToAbsoluteUrl(Urls.Account.ConfirmEmail)}?userId={userId}&code={code}&change={isChange}&returnUrl={parameters.ReturnUrl}";

        var paragraphs = new List<string>
        {
            $"Please confirm your account by <a href='{HtmlEncoder.Default.Encode(callbackUrl)}'>clicking here</a>."
        };

        await providerService.EmailProvider!.SendEmailWithTemplateAsync(email, "Confirm your email", paragraphs);
    }
}