using System.Text;
using System.Text.Encodings.Web;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using ZauberCMS.Core.Email.Commands;
using ZauberCMS.Core.Extensions;
using ZauberCMS.Core.Membership.Commands;
using ZauberCMS.Core.Membership.Models;
using ZauberCMS.Core.Providers;
using ZauberCMS.Core.Shared.Models;

namespace ZauberCMS.Core.Membership.Handlers
{
    public class ForgotPasswordHandler(
        IHttpContextAccessor httpContextAccessor,
        ProviderService providerService,
        IServiceProvider serviceProvider)
        : IRequestHandler<ForgotPasswordCommand, AuthenticationResult>
    {
        public async Task<AuthenticationResult> Handle(ForgotPasswordCommand request, CancellationToken cancellationToken)
        {
            var result = new AuthenticationResult();

            using var scope = serviceProvider.CreateScope();
            var mediatr = scope.ServiceProvider.GetRequiredService<IMediator>();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();

            if (request.Email != null)
            {
                var user = await userManager.FindByEmailAsync(request.Email);
                if (user != null)
                {
                    if (userManager.Options.SignIn.RequireConfirmedAccount && await userManager.IsEmailConfirmedAsync(user) == false)
                    {
                        result.Success = false;
                        result.AddMessage("Please check your email to confirm your account", ResultMessageType.Success);

                        // Resend confirmation email
                        await mediatr.Send(new SendEmailConfirmationCommand { ReturnUrl = "~/", User = user }, cancellationToken);
                        return result;
                    }

                    // For more information on how to enable account confirmation and password reset please
                    // visit https://go.microsoft.com/fwlink/?LinkID=532713
                    var code = await userManager.GeneratePasswordResetTokenAsync(user);
                    code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));
                    var callbackUrl = httpContextAccessor.ToAbsoluteUrl(Constants.Urls.Account.ResetPassword, new { code = code, email = request.Email });

                    var paragraphs = new List<string> { $"Please reset your password by <a class=\"underline\" href='{HtmlEncoder.Default.Encode(callbackUrl)}'>clicking here</a>." };
                    await providerService.EmailProvider!.SendEmailWithTemplateAsync(request.Email, "Reset Password", paragraphs);
                }
            }

            result.Success = true;
            result.AddMessage("An email has been sent to you to", ResultMessageType.Success);

            return result;
        }
    }
}