using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ZauberCMS.Core.Data;
using ZauberCMS.Core.Email.Interfaces;
using ZauberCMS.Core.Email.Parameters;
using ZauberCMS.Core.Extensions;
using ZauberCMS.Core.Membership.Models;
using ZauberCMS.Core.Membership.Parameters;
using ZauberCMS.Core.Plugins;
using ZauberCMS.Core.Providers;
using ZauberCMS.Core.Sections.Interfaces;
using ZauberCMS.Core.Settings;
using ZauberCMS.Core.Shared.Models;

namespace ZauberCMS.Core.Membership.Services;

// The account pages: sign-in, registration, external sign-in, email confirmation and passwords.
public partial class MembershipService
{
    /// <summary>
    /// Signs a user in with email/password and returns navigation outcome.
    /// </summary>
    /// <param name="parameters">Email, password and return url.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Authentication result.</returns>
    public async Task<AuthenticationResult> LoginUserAsync(LoginUserParameters parameters, CancellationToken cancellationToken = default)
    {
        using var scope = serviceScopeFactory.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var signInManager = scope.ServiceProvider.GetRequiredService<SignInManager<User>>();
        var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();
        var loginResult = new AuthenticationResult();
        
        try
        {
            await signInManager.SignOutAsync();
            var user = await userManager.FindByEmailAsync(parameters.Email);
            if (user != null && !user.Deleted)
            {
                var signInResult = await signInManager.PasswordSignInAsync(user, parameters.Password, parameters.RememberMe, lockoutOnFailure: true);
                loginResult.Success = signInResult.Succeeded;

                if (loginResult.Success)
                {
                    // Anyone who may open a section of the admin starts there (Roadmap D67), not only administrators
                    var userPrincipal = await signInManager.CreateUserPrincipalAsync(user);
                    if (parameters.ReturnUrl.IsNullOrWhiteSpace() &&
                        (await scope.ServiceProvider.GetRequiredService<IBackOfficeAccessService>()
                            .GetSectionsAsync(userPrincipal, cancellationToken)).Count > 0)
                    {
                        parameters.ReturnUrl = Urls.AdminBaseUrl;
                    }
                    loginResult.NavigateToUrl = parameters.ReturnUrl;
                }
                else
                {
                    if (signInResult.IsNotAllowed)
                    {
                        // Identity refuses an unconfirmed account before it looks at the password, so only someone
                        // who knows the password learns that the account is waiting for confirmation (ZA-106).
                        // A wrong password counts towards the lockout, as it does for any other account. Identity
                        // reports "not allowed" before it looks at the lockout, so the lockout is checked here: a
                        // locked account is not tried at all, or this would be a way round it.
                        if (await userManager.IsLockedOutAsync(user))
                        {
                            logger.LogWarning("User {UserId} account is locked out", user.Id);
                            loginResult.AddMessage("Too many failed sign-in attempts. Please wait a few minutes and try again.", ResultMessageType.Error);
                        }
                        else if (!await userManager.CheckPasswordAsync(user, parameters.Password))
                        {
                            await userManager.AccessFailedAsync(user);
                            loginResult.AddMessage("Invalid login credentials", ResultMessageType.Error);
                        }
                        else if (!await userManager.IsEmailConfirmedAsync(user))
                        {
                            await userManager.ResetAccessFailedCountAsync(user);
                            loginResult.AddMessage("Email isn't confirmed. Check your inbox for a confirmation email", ResultMessageType.Warning);
                            await ResendConfirmationAsync(scope.ServiceProvider, user, parameters.ReturnUrl, cancellationToken);
                        }
                        else
                        {
                            loginResult.AddMessage("Invalid login credentials", ResultMessageType.Error);
                        }
                    }
                    else if (signInResult.IsLockedOut)
                    {
                        // The user id, not the address typed in: no email addresses in the log (ZA-107)
                        logger.LogWarning("User {UserId} account is locked out", user.Id);
                        loginResult.AddMessage("Too many failed sign-in attempts. Please wait a few minutes and try again.", ResultMessageType.Error);
                    }
                    else if (signInResult.RequiresTwoFactor)
                    {
                        // This is currently not supported
                        loginResult.NavigateToUrl = $"{Urls.Account.LoginWith2Fa}?returnUrl={parameters.ReturnUrl}&rememberMe={parameters.RememberMe}";
                    }
                    else
                    {
                        loginResult.AddMessage("Invalid login credentials", ResultMessageType.Error);
                    }
                }
            }
            else
            {
                loginResult.AddMessage("Invalid login credentials", ResultMessageType.Error);
            }
        }
        catch (Exception e)
        {
           logger.LogError(e, "Error during login");
           loginResult.AddMessage("An unexpected error occurred. Please try again.", ResultMessageType.Error);
           loginResult.Success = false;
           return loginResult;
        }

        return loginResult;
    }

    /// <summary>
    /// Sends the confirmation email again, at most once per <see cref="ConfirmationResendInterval"/> for each account
    /// on this server. A send that fails is logged and does not count, so the next sign-in tries again.
    /// </summary>
    private async Task ResendConfirmationAsync(IServiceProvider services, User user, string? returnUrl,
        CancellationToken cancellationToken)
    {
        var memoryCache = services.GetRequiredService<IMemoryCache>();
        var resendKey = $"ZauberCMS.ConfirmationResent.{user.Id}";
        lock (ResendLock)
        {
            if (memoryCache.TryGetValue(resendKey, out _))
            {
                return;
            }

            memoryCache.Set(resendKey, true, ConfirmationResendInterval);
        }

        try
        {
            await emailService.SendEmailConfirmationAsync(new SendEmailConfirmationParameters
            {
                ReturnUrl = returnUrl,
                User = user
            }, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            memoryCache.Remove(resendKey);
            logger.LogError(ex, "Could not send the confirmation email again to user {UserId}", user.Id);
        }
    }

    /// <summary>
    /// Registers a new user and signs them in.
    /// </summary>
    /// <param name="parameters">User details and return url.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Authentication result.</returns>
    public async Task<AuthenticationResult> RegisterUserAsync(RegisterUserParameters parameters, CancellationToken cancellationToken = default)
    {
        using var scope = serviceScopeFactory.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var dbContext = scope.ServiceProvider.GetRequiredService<IZauberDbContext>();
        var signInManager = scope.ServiceProvider.GetRequiredService<SignInManager<User>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<Role>>();
        var registrationResult = new AuthenticationResult();

        try
        {
            var newUser = new User { Id = Guid.NewGuid().NewSequentialGuid(), Email = parameters.Email, UserName = parameters.Username };
            var result = await userManager.CreateAsync(newUser, parameters.Password);
            registrationResult.Success = result.Succeeded;
            
            if (registrationResult.Success)
            {
                // Add default role
                registrationResult = await userManager.AssignStartingRoleAsync(
                                        roleManager,
                                        logger,
                                        dbContext,
                                        settings,
                                        dataService,
                                        newUser,
                                        registrationResult);

                if (!registrationResult.Success)
                {
                    return registrationResult;
                }

                var user = await userManager.FindByEmailAsync(parameters.Email);

                // An address on the admin list (ZA-023). Whoever registered chose this account's password, and
                // that need not be the owner of the address. So the owner is not asked to confirm (a link that
                // anybody, or a mail scanner, can open would hand the registrant an administrator account):
                // they are sent a link to set the password themselves, and that is what promotes the account
                // (ResetPasswordAsync). The page says nothing that tells a stranger the address is on the list.
                var onAdminList = user is { EmailConfirmed: false } &&
                                  (await dataService.GetGlobalSettings()).IsAdminEmailAddress(user.Email);
                if (onAdminList)
                {
                    try
                    {
                        await SendSetPasswordEmailAsync(userManager, user!);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Could not send the set-password email to {UserName}, whose address is " +
                                            "on the admin list. They can ask for it with \"Forgot your password\"",
                            user!.UserName);
                    }
                }

                if (userManager.Options.SignIn.RequireConfirmedAccount)
                {
                    if (!onAdminList)
                    {
                        var sendConfirmationEmailCommand = new SendEmailConfirmationParameters
                        {
                            ReturnUrl = parameters.ReturnUrl,
                            User = user
                        };

                        await emailService.SendEmailConfirmationAsync(sendConfirmationEmailCommand, cancellationToken);
                    }

                    registrationResult.AddMessage("Please check your email and click the link to confirm your account", ResultMessageType.Success);
                }
                else
                {
                    if (parameters.AutoLogin)
                    {
                        var signInResult = await signInManager.PasswordSignInAsync(user!, parameters.Password, parameters.RememberMe, lockoutOnFailure: true);
                        registrationResult.Success = signInResult.Succeeded;
                    
                        if (parameters.ReturnUrl.IsNullOrWhiteSpace() &&
                            (await scope.ServiceProvider.GetRequiredService<IBackOfficeAccessService>()
                                .GetSectionsAsync(await signInManager.CreateUserPrincipalAsync(user!), cancellationToken)).Count > 0)
                        {
                            parameters.ReturnUrl = Urls.AdminBaseUrl;
                        }
                    }
                    
                    registrationResult.NavigateToUrl = parameters.ReturnUrl ?? "/";   
                    
                }
            }
            else
            {
                registrationResult.Messages.AddRange(result.Errors.Select(e => new ResultMessage(e.Description, ResultMessageType.Error)));
            }
            
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error during registration");
            registrationResult.AddMessage("An unexpected error occurred. Please try again.", ResultMessageType.Error);
            registrationResult.Success = false;
        }

        return registrationResult;
    }

    /// <summary>
    /// Signs in a user via external provider, creating a local account if needed.
    /// </summary>
    /// <param name="parameters">Return url.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Authentication result.</returns>
    public async Task<AuthenticationResult> ExternalLoginAsync(ExternalLoginParameters parameters, CancellationToken cancellationToken = default)
    {
        using var scope = serviceScopeFactory.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var signInManager = scope.ServiceProvider.GetRequiredService<SignInManager<User>>();
        var loginResult = new AuthenticationResult();

        try
        {
            var info = await signInManager.GetExternalLoginInfoAsync();
            if (info == null)
            {
                loginResult.AddMessage("Error loading external login information", ResultMessageType.Error);
                return loginResult;
            }

            var signInResult = await signInManager.ExternalLoginSignInAsync(info.LoginProvider, info.ProviderKey, isPersistent: false, bypassTwoFactor: true);
            if (signInResult.Succeeded)
            {
                loginResult.Success = true;
                loginResult.NavigateToUrl = parameters.ReturnUrl;
            }
            else if (signInResult.IsLockedOut)
            {
                loginResult.AddMessage("Account is locked out", ResultMessageType.Error);
            }
            else if (signInResult.IsNotAllowed)
            {
                // The account exists but may not sign in (deleted, or its email address is not confirmed)
                loginResult.AddMessage("This account cannot sign in", ResultMessageType.Error);
            }
            else
            {
                // ZauberSignInManager creates the account for a first sign-in, with its checks: registration on,
                // an email address the provider says is verified (ZA-105). A failure here means it refused; this
                // used to create the account anyway, without those checks.
                loginResult.AddMessage("Signing in with this provider did not work. Sign in another way, or " +
                                       "ask the site's administrator", ResultMessageType.Error);
            }
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error during external login");
            loginResult.AddMessage("An unexpected error occurred. Please try again.", ResultMessageType.Error);
            loginResult.Success = false;
        }

        return loginResult;
    }

    /// <summary>
    /// Confirms a user's email address.
    /// </summary>
    /// <param name="parameters">User id, confirmation code and return url.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Authentication result.</returns>
    public async Task<AuthenticationResult> ConfirmEmailAsync(ConfirmEmailParameters parameters, CancellationToken cancellationToken = default)
    {
        using var scope = serviceScopeFactory.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var confirmationResult = new AuthenticationResult();

        try
        {
            var user = await userManager.FindByIdAsync(parameters.UserId!);
            if (user == null)
            {
                confirmationResult.AddMessage("User not found", ResultMessageType.Error);
                return confirmationResult;
            }

            // The emailed link carries the token Base64Url-encoded (EmailService.SendEmailConfirmationAsync).
            string token;
            try
            {
                token = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(parameters.Code ?? string.Empty));
            }
            catch (FormatException)
            {
                confirmationResult.AddMessage("The confirmation link is not valid.", ResultMessageType.Error);
                return confirmationResult;
            }

            IdentityResult result;
            if (parameters.IsEmailUpdate)
            {
                if (parameters.NewEmailAddress.IsNullOrWhiteSpace())
                {
                    confirmationResult.AddMessage("The confirmation link is not valid.", ResultMessageType.Error);
                    return confirmationResult;
                }

                // Only the address asked for last (Roadmap D72): a link sent before it, for an address the user then
                // replaced (mistyped, say), is refused, so whoever reads that mailbox cannot take the account
                if (user.ExtendedData.TryGetValue(PendingEmailKey, out var pending) &&
                    !string.Equals(pending?.ToString(), parameters.NewEmailAddress, StringComparison.OrdinalIgnoreCase))
                {
                    confirmationResult.AddMessage(
                        "A newer link was sent for this account; this one no longer works. Use the newest email.",
                        ResultMessageType.Error);
                    return confirmationResult;
                }

                // Saved with the change below, which also stores nothing when the token is refused
                user.ExtendedData.Remove(PendingEmailKey);

                // The token was issued for this exact address, so a different address in the link is refused.
                result = await userManager.ChangeEmailAsync(user, parameters.NewEmailAddress!, token);
                if (result.Succeeded)
                {
                    await WriteAccountAuditAsync(scope.ServiceProvider, user, "changed their email address",
                        cancellationToken);
                }
            }
            else
            {
                result = await userManager.ConfirmEmailAsync(user, token);
            }

            if (result.Succeeded)
            {
                // Deliberately no promotion here for an address on the admin list: opening a link proves that
                // somebody saw the email, not that the account is theirs (see RegisterUserAsync)
                cacheService.ClearCachedItemsWithPrefix(nameof(User));
                confirmationResult.Success = true;
                confirmationResult.NavigateToUrl = parameters.ReturnUrl;
            }
            else
            {
                confirmationResult.Messages.AddRange(result.Errors.Select(e => new ResultMessage(e.Description, ResultMessageType.Error)));
            }
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error confirming email for user {UserId}", parameters.UserId);
            confirmationResult.AddMessage("An unexpected error occurred. Please try again.", ResultMessageType.Error);
            confirmationResult.Success = false;
        }

        return confirmationResult;
    }

    /// <summary>
    /// Starts the password reset flow by generating a token and emailing a link.
    /// </summary>
    /// <param name="parameters">Email and return url.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Authentication result.</returns>
    public async Task<AuthenticationResult> ForgotPasswordAsync(ForgotPasswordParameters parameters, CancellationToken cancellationToken = default)
    {
        var forgotPasswordResult = new AuthenticationResult();

        if (parameters.Email == null)
        {
            forgotPasswordResult.AddMessage("Email is missing", ResultMessageType.Error);
            forgotPasswordResult.Success = false;
            return forgotPasswordResult;
        }

        using var scope = serviceScopeFactory.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();

        try
        {
            var user = await userManager.FindByEmailAsync(parameters.Email);
            if (user != null && !user.Deleted)
            {
                if (userManager.Options.SignIn.RequireConfirmedAccount && !await userManager.IsEmailConfirmedAsync(user))
                {
                    // Send confirmation email out-of-band; do not vary the response so account
                    // existence / confirmation status cannot be inferred from the wording.
                    await emailService.SendEmailConfirmationAsync(new SendEmailConfirmationParameters { ReturnUrl = "~/", User = user }, cancellationToken);
                }
                else
                {
                    var code = await userManager.GeneratePasswordResetTokenAsync(user);
                    code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));
                    var callbackUrl = httpContextAccessor.ToAbsoluteUrl(Urls.Account.ResetPassword, new { code, email = parameters.Email });

                    var paragraphs = new List<string> { $"Please reset your password by <a class=\"underline\" href='{HtmlEncoder.Default.Encode(callbackUrl)}'>clicking here</a>." };
                    await providerService.EmailProvider!.SendEmailWithTemplateAsync(parameters.Email, "Reset Password", paragraphs);
                }
            }
        }
        catch (Exception e)
        {
            // Log but do not surface — varying the response on error is itself an enumeration channel.
            logger.LogError(e, "ForgotPasswordAsync failed");
        }

        forgotPasswordResult.Success = true;
        forgotPasswordResult.AddMessage("If the email matches an account, a password reset link has been sent.", ResultMessageType.Success);
        return forgotPasswordResult;
    }

    // The email for an address on the admin list that somebody registered with a password: the same link as
    // "Forgot your password", with words that explain why it arrived.
    private async Task SendSetPasswordEmailAsync(UserManager<User> userManager, User user)
    {
        var code = WebEncoders.Base64UrlEncode(
            Encoding.UTF8.GetBytes(await userManager.GeneratePasswordResetTokenAsync(user)));
        var callbackUrl = httpContextAccessor.ToAbsoluteUrl(Urls.Account.ResetPassword, new { code, email = user.Email });

        var paragraphs = new List<string>
        {
            "An account was registered on this site with your email address.",
            $"To finish setting it up, choose its password by <a class=\"underline\" href='{HtmlEncoder.Default.Encode(callbackUrl)}'>clicking here</a>.",
            "If you did not register, you can ignore this email."
        };
        await providerService.EmailProvider!.SendEmailWithTemplateAsync(user.Email!, "Finish setting up your account", paragraphs);
    }

    /// <summary>
    /// Completes the password reset using the provided token.
    /// </summary>
    /// <param name="parameters">Email, reset token and new password.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Authentication result.</returns>
    public async Task<AuthenticationResult> ResetPasswordAsync(ResetPasswordParameters parameters, CancellationToken cancellationToken = default)
    {
        using var scope = serviceScopeFactory.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var resetPasswordResult = new AuthenticationResult();

        try
        {
            if (!parameters.Email.IsNullOrWhiteSpace())
            {
                var user = await userManager.FindByEmailAsync(parameters.Email);
                if (user == null || user.Deleted)
                {
                    resetPasswordResult.AddMessage("User not found", ResultMessageType.Error);
                    return resetPasswordResult;
                }

                var result = await userManager.ResetPasswordAsync(user, parameters.Code!, parameters.Password!);
                if (result.Succeeded)
                {
                    // The code only ever goes to the account's email address, and the password is now one that
                    // its reader chose: the address is proven and the account is theirs. For an address on the
                    // admin list this is the moment it becomes an administrator (ZA-023).
                    if ((await dataService.GetGlobalSettings()).IsAdminEmailAddress(user.Email))
                    {
                        if (!user.EmailConfirmed)
                        {
                            user.EmailConfirmed = true;
                            await userManager.UpdateAsync(user);
                        }

                        await userManager.PromoteListedAdminAsync(
                            scope.ServiceProvider.GetRequiredService<RoleManager<Role>>(), logger, dataService, user);
                        cacheService.ClearCachedItemsWithPrefix(nameof(User));
                    }

                    resetPasswordResult.Success = true;
                    resetPasswordResult.NavigateToUrl = parameters.ReturnUrl;
                    resetPasswordResult.AddMessage($"Your password has been reset, <a class=\"underline\" href=\"{Urls.Account.Login}\">please login</a>", ResultMessageType.Success);
                }
                else
                {
                    resetPasswordResult.Success = false;
                    resetPasswordResult.Messages.AddRange(result.Errors.Select(e => new ResultMessage(e.Description, ResultMessageType.Error)));
                }   
            }
            else
            {
                resetPasswordResult.AddMessage("Email is empty", ResultMessageType.Error);
                resetPasswordResult.Success = false;
            }
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error resetting password");
            resetPasswordResult.AddMessage("An unexpected error occurred. Please try again.", ResultMessageType.Error);
            resetPasswordResult.Success = false;
        }

        return resetPasswordResult;
    }
}
