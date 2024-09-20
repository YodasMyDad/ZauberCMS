using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ZauberCMS.Core.Data;
using ZauberCMS.Core.Membership.Models;
using ZauberCMS.Core.Settings;
using ZauberCMS.Core.Shared.Models;

namespace ZauberCMS.Core.Extensions;

public static class RoleExtensions
{
        public static async Task<AuthenticationResult> AssignStartingRoleAsync(
            this UserManager<User> userManager,
            RoleManager<Role> roleManager,
            ILogger logger,
            ZauberDbContext dbContext,
            IOptions<ZauberSettings> settings,
            IMediator mediator,
            User newUser,
            AuthenticationResult loginResult)
        {
            // Log new account creation
            logger.LogInformation("{RequestUsername} created a new account", newUser.UserName);

            var globalSettings = await mediator.GetGlobalSettings();
            
            // Determine starting role name
            var startingRoleName = settings.Value.NewUserStartingRole ?? Constants.Roles.StandardRoleName;
            if (dbContext.Users.Count() == 1 || 
                globalSettings.AdminEmailAddresses.Count != 0 && 
                globalSettings.AdminEmailAddresses.Contains(newUser.Email!))
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
}