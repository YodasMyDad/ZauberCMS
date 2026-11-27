using System.Globalization;
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
    /// <summary>
    /// Claim an external provider must supply with the value "true" before an account is created from its email
    /// address. Google maps it from its user-info response; Facebook and Microsoft only supply it when
    /// <c>Zauber:Identity:ExternalProviders:&lt;Provider&gt;:TrustEmail</c> is true. A custom
    /// <c>IExternalAuthenticationProvider</c> adds it with <c>options.ClaimActions</c>.
    /// </summary>
    public const string EmailVerifiedClaimType = "email_verified";

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

            user = await CreateExternalUserAsync(info);
            if (user == null)
            {
                return SignInResult.Failed;
            }

            // The account, its login and its starting role exist now, so one sign-in carries the right claims.
            signInResult =
                await base.ExternalLoginSignInAsync(loginProvider, providerKey, isPersistent, bypassTwoFactor);
        }

        return signInResult;
    }

    /// <summary>
    /// Creates the local account for a first external sign-in: the user (email confirmed), the external login and
    /// the starting role. Returns null, and creates nothing, when registration is turned off
    /// (<c>GlobalSettings.AllowRegistration</c>), when the provider gave no email address or when it did not
    /// state that the address is verified (<see cref="EmailVerifiedClaimType"/>).
    /// Without that rule an account at the provider with somebody else's unverified address would become that
    /// person here, and an administrator if the address is in <c>GlobalSettings.AdminEmailAddresses</c>.
    /// </summary>
    public async Task<User?> CreateExternalUserAsync(ExternalLoginInfo info)
    {
        // A first sign-in through a provider is a registration: the switch that closes the page closes this too
        if (!(await dataService.GetGlobalSettings()).AllowRegistration)
        {
            logger.LogWarning("External login {LoginProvider}: registration is turned off; refusing to create account", info.LoginProvider);
            return null;
        }

        var email = info.Principal.FindFirstValue(ClaimTypes.Email);
        if (string.IsNullOrWhiteSpace(email))
        {
            logger.LogWarning("External login {LoginProvider} did not supply an email claim; refusing to create account", info.LoginProvider);
            return null;
        }

        if (!HasVerifiedEmail(info.Principal))
        {
            logger.LogWarning("External login {LoginProvider} did not state that the email address is verified; refusing to create account", info.LoginProvider);
            return null;
        }

        var user = new User
        {
            UserName = info.Principal.FindFirstValue(ClaimTypes.Name) ?? GenerateUsernameFromEmail(email),
            Email = email,
            EmailConfirmed = true
        };

        var createUserResult = await _userManager.CreateAsync(user);
        if (!createUserResult.Succeeded)
        {
            logger.LogError("Unable to create user for external login {LoginProvider}: {Errors}", info.LoginProvider,
                string.Join("; ", createUserResult.Errors.Select(e => e.Code)));
            return null;
        }

        var addLoginResult = await _userManager.AddLoginAsync(user,
            new UserLoginInfo(info.LoginProvider, info.ProviderKey, info.ProviderDisplayName ?? info.LoginProvider));
        if (!addLoginResult.Succeeded)
        {
            logger.LogError("Unable to add external login {LoginProvider} for user {UserUserName}", info.LoginProvider, user.UserName);
            // Nobody could ever sign in to the account, and its email address would block the next attempt.
            await _userManager.DeleteAsync(user);
            return null;
        }

        var roleResult = await _userManager.AssignStartingRoleAsync(
            roleManager,
            logger,
            dbContext,
            options,
            dataService,
            user,
            new AuthenticationResult { Success = true });
        if (!roleResult.Success)
        {
            logger.LogError("Unable to assign the starting role to user {UserUserName}: {Messages}", user.UserName,
                string.Join("; ", roleResult.Messages.Select(m => m.Message)));
            await _userManager.DeleteAsync(user);
            return null;
        }

        return user;
    }

    /// <summary>True when the provider supplied <see cref="EmailVerifiedClaimType"/> with the value "true".</summary>
    public static bool HasVerifiedEmail(ClaimsPrincipal principal) =>
        string.Equals(principal.FindFirstValue(EmailVerifiedClaimType), bool.TrueString,
            StringComparison.OrdinalIgnoreCase);

    private string? GenerateUsernameFromEmail(string? email)
    {
        if (!email.IsNullOrWhiteSpace())
        {
            var adjectives = new List<string> { "Brave", "Calm", "Eager", "Fancy", "Jolly", "Kind", "Lucky", "Silly", "Witty", "Zany" };
            var nouns = new List<string> { "Lion", "Tiger", "Bear", "Wolf", "Fox", "Hawk", "Shark", "Whale", "Eagle", "Frog" };

            // SHA-256, not MD5 (ZA-031): the hash only picks the words, but MD5 should not be used for anything new
            var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(email));

            var adjectiveIndex = BitConverter.ToUInt16(hashBytes, 0) % adjectives.Count;
            var nounIndex = BitConverter.ToUInt16(hashBytes, 2) % nouns.Count;
            var number = BitConverter.ToUInt16(hashBytes, 4) % 100;

            var username = adjectives[adjectiveIndex] + nouns[nounIndex] + number.ToString("D2", CultureInfo.InvariantCulture);

            // Optionally check for uniqueness and adjust if necessary. Includes deleted
            // users — the underlying UserName unique index does not filter on Deleted, so
            // skipping deleted matches here would let CreateAsync hit a constraint violation.
            var attempt = 1;
            while (_userManager.Users.Any(u => u.UserName == username))
            {
                number = (number + attempt) % 100;
                username = adjectives[adjectiveIndex] + nouns[nounIndex] + number.ToString("D2", CultureInfo.InvariantCulture);
                attempt++;
            }

            return username;
        }

        return email;
    }
}