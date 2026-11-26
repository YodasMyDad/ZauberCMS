using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ZauberCMS.Core.Data;
using ZauberCMS.Core.Data.Interfaces;
using ZauberCMS.Core.Membership.Models;
using ZauberCMS.Core.Settings;
using ZauberCMS.Core.Shared.Models;

namespace ZauberCMS.Core.Extensions;

public static class RoleExtensions
{
        /// <summary>
        /// Gives a new account its first role: <c>Zauber:NewUserStartingRole</c>, or the Admin role when the
        /// account's email address is confirmed and listed in <c>GlobalSettings.AdminEmailAddresses</c>.
        /// <para>
        /// The first account of a site is no longer made an administrator: the installer creates the
        /// administrator. Only an external sign-up arrives here confirmed (the provider vouches for the
        /// address). A password registration with a listed address gets the starting role and is promoted when
        /// the owner of the address sets the account's password from an emailed link
        /// (<see cref="PromoteListedAdminAsync"/>).
        /// </para>
        /// </summary>
        public static async Task<AuthenticationResult> AssignStartingRoleAsync(
            this UserManager<User> userManager,
            RoleManager<Role> roleManager,
            ILogger logger,
            IZauberDbContext dbContext,
            IOptions<ZauberSettings> settings,
            IDataService dataService,
            User newUser,
            AuthenticationResult loginResult)
        {
            // Log new account creation
            logger.LogInformation("{RequestUsername} created a new account", newUser.UserName);

            var globalSettings = await dataService.GetGlobalSettings();

            // Determine starting role name
            var startingRoleName = settings.Value.NewUserStartingRole ?? Constants.Roles.StandardRoleName;
            if (newUser.EmailConfirmed && globalSettings.IsAdminEmailAddress(newUser.Email))
            {
                startingRoleName = Constants.Roles.AdminRoleName;
            }

            // Check if the role exists
            var roleExist = await roleManager.RoleExistsAsync(startingRoleName);
            if (!roleExist)
            {
                await roleManager.CreateAsync(new Role { Name = startingRoleName });
            }

            // Add user to role
            var addToRoleResult = await userManager.AddToRoleAsync(newUser, startingRoleName);
            if (addToRoleResult.Succeeded == false)
            {
                addToRoleResult.LogErrors();
                loginResult.AddMessage(addToRoleResult.ToErrorsList(), ResultMessageType.Error);
                loginResult.Success = false;
                return loginResult;
            }

            return loginResult;
        }

        /// <summary>
        /// Adds <paramref name="user"/> to the Admin role when their email address is confirmed and listed in
        /// <c>GlobalSettings.AdminEmailAddresses</c>. Returns true when the user was promoted by this call.
        /// <para>
        /// Call it only when the person holding the account has proven the address is theirs: a password they
        /// set from an emailed link. A confirmation link being opened is not that proof, because the account's
        /// password may belong to somebody else who registered with the address.
        /// </para>
        /// </summary>
        public static async Task<bool> PromoteListedAdminAsync(
            this UserManager<User> userManager,
            RoleManager<Role> roleManager,
            ILogger logger,
            IDataService dataService,
            User user)
        {
            if (!user.EmailConfirmed || user.Deleted)
            {
                return false;
            }

            var globalSettings = await dataService.GetGlobalSettings();
            if (!globalSettings.IsAdminEmailAddress(user.Email) ||
                await userManager.IsInRoleAsync(user, Constants.Roles.AdminRoleName))
            {
                return false;
            }

            if (!await roleManager.RoleExistsAsync(Constants.Roles.AdminRoleName))
            {
                await roleManager.CreateAsync(new Role { Name = Constants.Roles.AdminRoleName });
            }

            var result = await userManager.AddToRoleAsync(user, Constants.Roles.AdminRoleName);
            if (!result.Succeeded)
            {
                result.LogErrors();
                return false;
            }

            logger.LogInformation("{UserName} confirmed an email address on the admin list and is now an administrator",
                user.UserName);
            return true;
        }

        /// <summary>
        /// Whether <paramref name="email"/> is in <c>AdminEmailAddresses</c>. Email addresses are compared without
        /// regard to case or surrounding spaces.
        /// </summary>
        public static bool IsAdminEmailAddress(this GlobalSettings globalSettings, string? email)
        {
            var address = email?.Trim();
            return !string.IsNullOrEmpty(address) && globalSettings.AdminEmailAddresses.Any(x =>
                string.Equals(x?.Trim(), address, StringComparison.OrdinalIgnoreCase));
        }
}
