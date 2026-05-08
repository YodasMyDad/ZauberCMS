using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ZauberCMS.Core.Data;
using ZauberCMS.Core.Data.Interfaces;
using ZauberCMS.Core.Extensions;
using ZauberCMS.Core.Membership.Models;
using ZauberCMS.Core.Settings;

namespace ZauberCMS.Core.Membership;

public class ZauberSignInManager(
    UserManager<User> userManager,
    IHttpContextAccessor contextAccessor,
    IUserClaimsPrincipalFactory<User> claimsFactory,
    IOptions<IdentityOptions> optionsAccessor,
    ILogger<ZauberSignInManager> logger,
    IAuthenticationSchemeProvider schemes,
    IUserConfirmation<User> confirmation,
    IOptions<ZauberSettings> options,
    IDataService dataService,
    IZauberDbContext dbContext,
    RoleManager<Role> roleManager)
    : SignInManager<User>(userManager, contextAccessor, claimsFactory, optionsAccessor, logger, schemes, confirmation)
{
    private readonly UserManager<User> _userManager = userManager;

    public override Task<bool> CanSignInAsync(User user)
    {
        if (user.Deleted)
        {
            return Task.FromResult(false);
        }
        return base.CanSignInAsync(user);
    }

    public override async Task<SignInResult> ExternalLoginSignInAsync(string loginProvider, string providerKey,
        bool isPersistent, bool bypassTwoFactor)
    {
        var signInResult =
            await base.ExternalLoginSignInAsync(loginProvider, providerKey, isPersistent, bypassTwoFactor);

        var user = await _userManager.FindByLoginAsync(loginProvider, providerKey);

        // If the user doesn't exist, create them.
        if (user == null)
        {
            var info = await GetExternalLoginInfoAsync();
            if (info == null)
            {
                logger.LogError("Error loading external login information");
                return SignInResult.Failed;
            }

            var email = info.Principal.FindFirstValue(ClaimTypes.Email);
            if (string.IsNullOrWhiteSpace(email))
            {
                logger.LogWarning("External login {LoginProvider} did not supply an email claim; refusing to create account", loginProvider);
                return SignInResult.Failed;
            }

            // Require the IdP to assert that the email is verified (Google/Microsoft/Facebook
            // all surface this as the standard "email_verified" claim, OIDC compliant or
            // OAuth-mapped). Without this check, an attacker controlling an IdP account with
            // an unverified `email` claim could impersonate a different user — and if the
            // email matches GlobalSettings.AdminEmailAddresses, gain Admin on first sign-in.
            var emailVerified = info.Principal.FindFirstValue("email_verified");
            if (!string.Equals(emailVerified, "true", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(emailVerified, bool.TrueString, StringComparison.OrdinalIgnoreCase))
            {
                logger.LogWarning("External login {LoginProvider} reported unverified email for {Email}; refusing to create account", loginProvider, email);
                return SignInResult.Failed;
            }

            var username = info.Principal.FindFirstValue(ClaimTypes.Name) ?? GenerateUsernameFromEmail(email);

            user = new User
            {
                UserName = username,
                Email = email,
                EmailConfirmed = true
            };

            var createUserResult = await _userManager.CreateAsync(user);

            if (!createUserResult.Succeeded)
            {
                // Handle failure to create user here.
                logger.LogError("Unable to create user for external login {LoginProvider}", loginProvider);
                return SignInResult.Failed;
            }

            var addLoginResult =
                await _userManager.AddLoginAsync(user, new UserLoginInfo(loginProvider, providerKey, loginProvider));

            if (!addLoginResult.Succeeded)
            {
                // Handle failure to associate login with user here.
                logger.LogError("Unable to add external login for user {UserUserName}", user.UserName);
                return SignInResult.Failed;
            }

            signInResult =
                await base.ExternalLoginSignInAsync(loginProvider, providerKey, isPersistent, bypassTwoFactor);
            
            // If the user successfully signed in, add them to a role.
            if (signInResult.Succeeded)
            {
                var loginResult = await _userManager.AssignStartingRoleAsync(
                    roleManager,
                    logger,
                    dbContext,
                    options,
                    dataService,
                    user,
                    new AuthenticationResult{Success = true});

                if (!loginResult.Success)
                {
                    logger.LogError("Unable to assign roles for login for user {UserUserName}", user.UserName);
                    foreach (var message in loginResult.Messages)
                    {
                        logger.LogError(message.Message);
                    }
                    return SignInResult.Failed;
                }
                
                // Sign in the user again to update their claims.
                await base.SignOutAsync();
                signInResult = await base.ExternalLoginSignInAsync(loginProvider, providerKey, isPersistent, bypassTwoFactor);
            }
        }

        return signInResult;
    }
    
    private string? GenerateUsernameFromEmail(string? email)
    {
        if (!email.IsNullOrWhiteSpace())
        {
            var adjectives = new List<string> { "Brave", "Calm", "Eager", "Fancy", "Jolly", "Kind", "Lucky", "Silly", "Witty", "Zany" };
            var nouns = new List<string> { "Lion", "Tiger", "Bear", "Wolf", "Fox", "Hawk", "Shark", "Whale", "Eagle", "Frog" };

            using var md5 = MD5.Create();
            var emailBytes = Encoding.UTF8.GetBytes(email);
            var hashBytes = md5.ComputeHash(emailBytes);

            var adjectiveIndex = BitConverter.ToUInt16(hashBytes, 0) % adjectives.Count;
            var nounIndex = BitConverter.ToUInt16(hashBytes, 2) % nouns.Count;
            var number = BitConverter.ToUInt16(hashBytes, 4) % 100;

            var username = adjectives[adjectiveIndex] + nouns[nounIndex] + number.ToString("D2");

            // Optionally check for uniqueness and adjust if necessary. Includes deleted
            // users — the underlying UserName unique index does not filter on Deleted, so
            // skipping deleted matches here would let CreateAsync hit a constraint violation.
            var attempt = 1;
            while (_userManager.Users.Any(u => u.UserName == username))
            {
                number = (number + attempt) % 100;
                username = adjectives[adjectiveIndex] + nouns[nounIndex] + number.ToString("D2");
                attempt++;
            }

            return username;
        }

        return email;
    }
}