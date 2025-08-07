using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using ZauberCMS.Core.Email.Interfaces;
using ZauberCMS.Core.Email.Parameters;
using ZauberCMS.Core.Extensions;
using ZauberCMS.Core.Membership.Models;
using ZauberCMS.Core.Providers;
using ZauberCMS.Core.Shared.Models;

namespace ZauberCMS.Core.Email.Services;

public class EmailService(
    IServiceProvider serviceProvider,
    ProviderService providerService,
    IHttpContextAccessor httpContextAccessor) : IEmailService
{
    public async Task<HandlerResult<object>> SendEmailConfirmationAsync(SendEmailConfirmationParameters parameters, CancellationToken cancellationToken = default)
    {
        using var scope = serviceProvider.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var handlerResult = new HandlerResult<object>();

        if (!parameters.UserId.IsNullOrWhiteSpace())
        {
            var user = await userManager.FindByIdAsync(parameters.UserId);
            if (user != null)
            {
                var token = await userManager.GenerateEmailConfirmationTokenAsync(user);
                var callbackUrl = httpContextAccessor.ToAbsoluteUrl(Urls.Account.ConfirmEmail, new { userId = parameters.UserId, code = token });

                var paragraphs = new List<string> 
                { 
                    $"Please confirm your account by <a class=\"underline\" href=\"{callbackUrl}\">clicking here</a>." 
                };

                await providerService.EmailProvider!.SendEmailWithTemplateAsync(
                    parameters.Email ?? user.Email, 
                    "Confirm your email", 
                    paragraphs);

                handlerResult.Success = true;
                handlerResult.AddMessage("Confirmation email sent", ResultMessageType.Success);
            }
            else
            {
                handlerResult.AddMessage("User not found", ResultMessageType.Error);
            }
        }
        else
        {
            handlerResult.AddMessage("User ID is required", ResultMessageType.Error);
        }

        return handlerResult;
    }
}