using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using ZauberCMS.Core.Extensions;
using ZauberCMS.Core.Membership.Commands;
using ZauberCMS.Core.Membership.Models;
using ZauberCMS.Core.Shared.Models;

namespace ZauberCMS.Core.Membership.Handlers
{
    public class ResetPasswordHandler(IServiceProvider serviceProvider)
        : IRequestHandler<ResetPasswordCommand, AuthenticationResult>
    {
        public async Task<AuthenticationResult> Handle(ResetPasswordCommand request, CancellationToken cancellationToken)
        {
            var result = new AuthenticationResult();
            using var scope = serviceProvider.CreateScope();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
            var user = await userManager.FindByEmailAsync(request.Email);
            if (user != null)
            {
                var resetResult = await userManager.ResetPasswordAsync(user, request.Code, request.Password);
                if (resetResult.Succeeded == false)
                {
                    result.Success = false;
                    foreach (var error in resetResult.Errors)
                    {
                        result.AddMessage(error.Description, ResultMessageType.Error);
                    }
                    return result;
                }
            }

            result.Success = true;
            result.AddMessage($"Your password has been reset, <a class=\"underline\" href=\"{Constants.Urls.Account.Login}\">please login</a>", ResultMessageType.Success);
            return result;
        }
    }
}