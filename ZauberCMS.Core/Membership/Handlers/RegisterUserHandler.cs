using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ZauberCMS.Core.Data;
using ZauberCMS.Core.Email.Commands;
using ZauberCMS.Core.Extensions;
using ZauberCMS.Core.Membership.Commands;
using ZauberCMS.Core.Membership.Models;
using ZauberCMS.Core.Settings;
using ZauberCMS.Core.Shared.Models;

namespace ZauberCMS.Core.Membership.Handlers;

public class RegisterUserHandler(
    ILogger<RegisterUserHandler> logger,
    IOptions<ZauberSettings> settings,
    IServiceProvider serviceProvider)
    : IRequestHandler<RegisterUserCommand, AuthenticationResult>
{
    public async Task<AuthenticationResult> Handle(RegisterUserCommand request, CancellationToken cancellationToken)
    {
        using var scope = serviceProvider.CreateScope();
        var mediatr = scope.ServiceProvider.GetRequiredService<IMediator>();
        var dbContext = scope.ServiceProvider.GetRequiredService<ZauberDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var signInManager = scope.ServiceProvider.GetRequiredService<SignInManager<User>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<Role>>();
            
        var newUser = new User { Id = Guid.NewGuid().NewSequentialGuid(), Email = request.Email, UserName = request.Username };
        var loginResult = new AuthenticationResult();
        var createResult = 
            await userManager.CreateAsync(newUser, request.Password);

        loginResult.Success = createResult.Succeeded;
        if (loginResult.Success)
        {
            loginResult = await userManager.AssignStartingRoleAsync(
                                            roleManager,
                                            logger,
                                            dbContext,
                                            settings,
                                            mediatr,
                                            newUser,
                                            loginResult);

            if (!loginResult.Success)
            {
                return loginResult;
            }

            var user = await userManager.FindByEmailAsync(request.Email);

            if (userManager.Options.SignIn.RequireConfirmedAccount)
            {
                var sendConfirmationEmailCommand = new SendEmailConfirmationCommand
                {
                    ReturnUrl = request.ReturnUrl,
                    User = user
                };

                await mediatr.Send(sendConfirmationEmailCommand, cancellationToken);

                loginResult.AddMessage("Please check your email and click the link to confirm your account", ResultMessageType.Success);
            }
            else
            {
                if (request.AutoLogin)
                {
                    var signInResult = await signInManager.PasswordSignInAsync(user!, request.Password, request.RememberMe, false);
                    loginResult.Success = signInResult.Succeeded;
                    
                    if (request.ReturnUrl.IsNullOrWhiteSpace() && await userManager.IsInRoleAsync(user!, Constants.Roles.AdminRoleName))
                    {
                        request.ReturnUrl = Urls.AdminBaseUrl;
                    }
                }
                    
                loginResult.NavigateToUrl = request.ReturnUrl ?? "/";   
                    
            }
        }
        else
        {
            createResult.LogErrors();
            loginResult.AddMessage(createResult.ToErrorsList(), ResultMessageType.Error);
        }

        return loginResult;
    }
}