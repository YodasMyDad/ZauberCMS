using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ZauberCMS.Core.Email.Commands;
using ZauberCMS.Core.Extensions;
using ZauberCMS.Core.Membership.Commands;
using ZauberCMS.Core.Membership.Models;
using ZauberCMS.Core.Shared.Models;

namespace ZauberCMS.Core.Membership.Handlers
{
    /// <summary>
    /// Responsible for handling a user login
    /// </summary>
    public class LoginUserHandler : IRequestHandler<LoginUserCommand, AuthenticationResult>
    {
        private readonly ILogger<LoginUserHandler> _logger;
        private readonly IServiceProvider _serviceProvider;
        
        public LoginUserHandler(ILogger<LoginUserHandler> logger, IServiceProvider serviceProvider)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
        }

        public async Task<AuthenticationResult> Handle(LoginUserCommand request, CancellationToken cancellationToken)
        {
            using var scope = _serviceProvider.CreateScope();
            var mediatr = scope.ServiceProvider.GetRequiredService<IMediator>();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
            var signInManager = scope.ServiceProvider.GetRequiredService<SignInManager<User>>();
            
            // This doesn't count login failures towards account lockout
            // To enable password failures to trigger account lockout, set lockoutOnFailure: true
            var loginResult = new AuthenticationResult();
            await signInManager.SignOutAsync();
            var user = await userManager.FindByEmailAsync(request.Email);
            if (user != null)
            {
                var signInResult = await signInManager.PasswordSignInAsync(user, request.Password, request.RememberMe, false);
                loginResult.Success = signInResult.Succeeded;

                if (loginResult.Success)
                {
                    var userPrincipal = await signInManager.CreateUserPrincipalAsync(user);
                    loginResult.NavigateToUrl = request.ReturnUrl ?? "~/";
                }
                else
                {
                    if (signInResult.IsNotAllowed)
                    {
                        if (!await userManager.IsEmailConfirmedAsync(user))
                        {
                            loginResult.AddMessage("Email isn't confirmed. Check your inbox for a confirmation email", ResultMessageType.Warning);

                            // Resend confirmation email
                            var sendConfirmationEmailCommand = new SendEmailConfirmationCommand
                            {
                                ReturnUrl = request.ReturnUrl,
                                User = user
                            };

                            await mediatr.Send(sendConfirmationEmailCommand, cancellationToken);
                        }

                        //if (!await _userManager.IsPhoneNumberConfirmedAsync(user))
                        //{
                        //      // Phone Number isn't confirmed.
                        //      // loginResult.FailedReason = "Phone Number isn't confirmed";
                        //}
                    }
                    else if (signInResult.IsLockedOut)
                    {
                        _logger.LogWarning("User {RequestEmail} account is locked out", request.Email);
                        loginResult.AddMessage("Account is locked out.", ResultMessageType.Error);
                    }
                    else if (signInResult.RequiresTwoFactor)
                    {
                        loginResult.NavigateToUrl = $"/account/loginwith2fa?returnUrl={request.ReturnUrl}&rememberMe={request.RememberMe}";
                    }
                    else
                    {
                        loginResult.AddMessage("Password is incorrect", ResultMessageType.Error);
                    }
                }
            }
            else
            {
                loginResult.AddMessage("You are do not have an account, please register", ResultMessageType.Error);
            }

            return loginResult;
        }
    }
}