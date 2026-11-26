using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ZauberCMS.Core.Data;
using ZauberCMS.Core.Data.Interfaces;
using ZauberCMS.Core.Email.Interfaces;
using ZauberCMS.Core.Email.Parameters;
using ZauberCMS.Core.Email.Services;
using ZauberCMS.Core.Extensions;
using ZauberCMS.Core.Membership.Interfaces;
using ZauberCMS.Core.Membership.Models;
using ZauberCMS.Core.Membership.Parameters;
using ZauberCMS.Core.Membership.Mapping;
using ZauberCMS.Core.Plugins;
using ZauberCMS.Core.Providers;
using ZauberCMS.Core.Settings;
using ZauberCMS.Core.Shared.Models;
using ZauberCMS.Core.Shared.Services;

namespace ZauberCMS.Core.Membership.Services;

public class MembershipService(
    IServiceScopeFactory serviceScopeFactory,
    ICacheService cacheService,
    IHttpContextAccessor httpContextAccessor,
    ICurrentUserAccessor currentUserAccessor,
    ExtensionManager extensionManager,
    IDataService dataService,
    ProviderService providerService,
    IEmailService emailService,
    IOptions<ZauberSettings> settings,
    ILogger<MembershipService> logger)
    : IMembershipService
{
    /// <summary>
    /// Retrieves a single user with roles and property data, optionally from cache.
    /// </summary>
    /// <param name="parameters">User id and caching flag.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>User or null.</returns>
    public async Task<User?> GetUserAsync(GetUserParameters parameters, CancellationToken cancellationToken = default)
    {
        using var scope = serviceScopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IZauberDbContext>();
        var query = BuildQuery(parameters, dbContext);
        if (parameters.Cached)
        {
            var cacheKey = query.GenerateCacheKey<User>();
            return await cacheService.GetSetCachedItemAsync(cacheKey, async () => await query.FirstOrDefaultAsync(cancellationToken: cancellationToken));
        }

        return await query.FirstOrDefaultAsync(cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Updates core properties, email/username and roles for an existing user. Also persists user property values.
    /// </summary>
    /// <param name="parameters">User to update and optional password/roles.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result including success and messages.</returns>
    public async Task<HandlerResult<User>> SaveUserAsync(SaveUserParameters parameters, CancellationToken cancellationToken = default)
    {
        using var scope = serviceScopeFactory.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        // Whether the caller edits their own account: then their sign-in is refreshed with the new name or address.
        // Through the user, not the claim: a caller signed in by another scheme may carry an id that is not a GUID.
        var callerId = (await currentUserAccessor.GetUserAsync(cancellationToken))?.Id;
        var dbContext = scope.ServiceProvider.GetRequiredService<IZauberDbContext>();
        var refreshCurrentUser = false;
        var isUpdate = false;
        var handlerResult = new HandlerResult<User>();
        
        if (parameters.User != null)
        {
            var user = await userManager.FindByIdAsync(parameters.User.Id.ToString());

            if (user == null)
            {
                user = parameters.User;
                var result = await userManager.CreateAsync(user, parameters.Password!);
                if (!result.Succeeded)
                {
                    handlerResult.Messages.AddRange(result.Errors.Select(e => new ResultMessage(e.Description, ResultMessageType.Error)));
                    return handlerResult;
                }

                // set the default starting role if no roles are set
                parameters.Roles ??= [Constants.Roles.StandardRoleName];
            }
            else
            {
                isUpdate = true;
                if (user.UserName != parameters.User.UserName)
                {
                    var result = await userManager.SetUserNameAsync(user, parameters.User.UserName);
                    if (!result.Succeeded)
                    {
                        handlerResult.Messages.AddRange(result.Errors.Select(e => new ResultMessage(e.Description, ResultMessageType.Error)));
                        return handlerResult;
                    }
                    if (callerId == user.Id)
                    {
                        refreshCurrentUser = true;   
                    }
                }

                if (user.Email != parameters.User.Email)
                {
                    var result = await userManager.SetEmailAsync(user, parameters.User.Email);
                    if (!result.Succeeded)
                    {
                        handlerResult.Messages.AddRange(result.Errors.Select(e => new ResultMessage(e.Description, ResultMessageType.Error)));
                        return handlerResult;
                    }
                    if (callerId == user.Id)
                    {
                        refreshCurrentUser = true;   
                    }
                }

                // Update other properties
                parameters.User.MapTo(user);
                user.DateUpdated = DateTime.UtcNow;
                
                var updateResult = await userManager.UpdateAsync(user);
                if (!updateResult.Succeeded)
                {
                    handlerResult.Messages.AddRange(updateResult.Errors.Select(e => new ResultMessage(e.Description, ResultMessageType.Error)));
                    return handlerResult;
                }
                
                // Note: Audit logging would need to be implemented without mediator
                logger.LogInformation("Audit logging for user {UserName} {Action}", parameters.User.Name, isUpdate ? "Update" : "Create");
                
                // Finally update property data
                handlerResult = await UpdateUserPropertyValues(dbContext, parameters.User, handlerResult, cancellationToken);
            }

            // Handle roles
            if (parameters.Roles != null)
            {
                var currentRoles = await userManager.GetRolesAsync(user);
                var rolesToAdd = parameters.Roles.Except(currentRoles).ToList();
                var rolesToRemove = currentRoles.Except(parameters.Roles).ToList();

                if (rolesToAdd.Count != 0)
                {
                    var result = await userManager.AddToRolesAsync(user, rolesToAdd);
                    if (!result.Succeeded)
                    {
                        handlerResult.Messages.AddRange(result.Errors.Select(e => new ResultMessage(e.Description, ResultMessageType.Error)));
                        return handlerResult;
                    }

                    if (callerId == user.Id)
                    {
                        refreshCurrentUser = true;   
                    }
                }

                if (rolesToRemove.Count != 0)
                {
                    var result = await userManager.RemoveFromRolesAsync(user, rolesToRemove);
                    if (!result.Succeeded)
                    {
                        handlerResult.Messages.AddRange(result.Errors.Select(e => new ResultMessage(e.Description, ResultMessageType.Error)));
                        return handlerResult;
                    }
                    
                    if (callerId == user.Id)
                    {
                        refreshCurrentUser = true;   
                    }
                }
            }

            // Update security stamp if needed
            if (refreshCurrentUser == false && userManager.SupportsUserSecurityStamp)
            {
                await userManager.UpdateSecurityStampAsync(user);
            }

            handlerResult.Entity = user;
            handlerResult.Success = true;
            handlerResult.RefreshSignIn = refreshCurrentUser;
            if (refreshCurrentUser)
            {
                handlerResult.RefreshSignInTicket =
                    scope.ServiceProvider.GetRequiredService<RefreshSignInTicketService>().Create(user);
            }
        }
        else
        {
            handlerResult.Messages.Add(new ResultMessage("User is null", ResultMessageType.Error));
        }

        return handlerResult;
    }

    /// <summary>
    /// Creates a new user or updates an existing one, including roles and property values.
    /// </summary>
    /// <param name="parameters">User, password (for create), and role set.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result including success and messages.</returns>
    public async Task<HandlerResult<User>> CreateUpdateUserAsync(CreateUpdateUserParameters parameters, CancellationToken cancellationToken = default)
    {
        using var scope = serviceScopeFactory.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        // Whether the caller edits their own account: then their sign-in is refreshed with the new name or address.
        // Through the user, not the claim: a caller signed in by another scheme may carry an id that is not a GUID.
        var callerId = (await currentUserAccessor.GetUserAsync(cancellationToken))?.Id;
        var dbContext = scope.ServiceProvider.GetRequiredService<IZauberDbContext>();
        var refreshCurrentUser = false;
        var isUpdate = false;
        var handlerResult = new HandlerResult<User>();
        
        if (parameters.User != null)
        {
            var user = await userManager.FindByIdAsync(parameters.User.Id.ToString());

            if (user == null)
            {
                user = parameters.User;
                var result = await userManager.CreateAsync(user, parameters.Password!);
                if (!result.Succeeded)
                {
                    handlerResult.Messages.AddRange(result.Errors.Select(e => new ResultMessage(e.Description, ResultMessageType.Error)));
                    return handlerResult;
                }

                // set the default starting role if no roles are set
                parameters.Roles ??= [Constants.Roles.StandardRoleName];
            }
            else
            {
                isUpdate = true;
                if (user.UserName != parameters.User.UserName)
                {
                    var result = await userManager.SetUserNameAsync(user, parameters.User.UserName);
                    if (!result.Succeeded)
                    {
                        handlerResult.Messages.AddRange(result.Errors.Select(e => new ResultMessage(e.Description, ResultMessageType.Error)));
                        return handlerResult;
                    }
                    if (callerId == user.Id)
                    {
                        refreshCurrentUser = true;   
                    }
                }

                if (user.Email != parameters.User.Email)
                {
                    var result = await userManager.SetEmailAsync(user, parameters.User.Email);
                    if (!result.Succeeded)
                    {
                        handlerResult.Messages.AddRange(result.Errors.Select(e => new ResultMessage(e.Description, ResultMessageType.Error)));
                        return handlerResult;
                    }
                    if (callerId == user.Id)
                    {
                        refreshCurrentUser = true;   
                    }
                }

                // Update other properties
                parameters.User.MapTo(user);
                user.DateUpdated = DateTime.UtcNow;
                
                var updateResult = await userManager.UpdateAsync(user);
                if (!updateResult.Succeeded)
                {
                    handlerResult.Messages.AddRange(updateResult.Errors.Select(e => new ResultMessage(e.Description, ResultMessageType.Error)));
                    return handlerResult;
                }
                
                // Note: Audit logging would need to be implemented without mediator
                logger.LogInformation("Audit logging for user {UserName} {Action}", parameters.User.Name, isUpdate ? "Update" : "Create");
                
                // Finally update property data
                handlerResult = await UpdateUserPropertyValues(dbContext, parameters.User, handlerResult, cancellationToken);
            }

            // Handle roles
            if (parameters.Roles != null)
            {
                var currentRoles = await userManager.GetRolesAsync(user);
                var rolesToAdd = parameters.Roles.Except(currentRoles).ToList();
                var rolesToRemove = currentRoles.Except(parameters.Roles).ToList();

                if (rolesToAdd.Count != 0)
                {
                    var result = await userManager.AddToRolesAsync(user, rolesToAdd);
                    if (!result.Succeeded)
                    {
                        handlerResult.Messages.AddRange(result.Errors.Select(e => new ResultMessage(e.Description, ResultMessageType.Error)));
                        return handlerResult;
                    }

                    if (callerId == user.Id)
                    {
                        refreshCurrentUser = true;   
                    }
                }

                if (rolesToRemove.Count != 0)
                {
                    var result = await userManager.RemoveFromRolesAsync(user, rolesToRemove);
                    if (!result.Succeeded)
                    {
                        handlerResult.Messages.AddRange(result.Errors.Select(e => new ResultMessage(e.Description, ResultMessageType.Error)));
                        return handlerResult;
                    }
                    
                    if (callerId == user.Id)
                    {
                        refreshCurrentUser = true;   
                    }
                }
            }

            // Update security stamp if needed
            if (refreshCurrentUser == false && userManager.SupportsUserSecurityStamp)
            {
                await userManager.UpdateSecurityStampAsync(user);
            }

            handlerResult.Entity = user;
            handlerResult.Success = true;
            handlerResult.RefreshSignIn = refreshCurrentUser;
            if (refreshCurrentUser)
            {
                handlerResult.RefreshSignInTicket =
                    scope.ServiceProvider.GetRequiredService<RefreshSignInTicketService>().Create(user);
            }
        }
        else
        {
            handlerResult.Messages.Add(new ResultMessage("User is null", ResultMessageType.Error));
        }

        return handlerResult;
    }

    /// <summary>
    /// Deletes a user account.
    /// </summary>
    /// <param name="parameters">Parameters containing the user id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result including success and messages.</returns>
    public async Task<HandlerResult<User>> DeleteUserAsync(DeleteUserParameters parameters, CancellationToken cancellationToken = default)
    {
        using var scope = serviceScopeFactory.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var handlerResult = new HandlerResult<User>();

#pragma warning disable CS0618 // UserId is the obsolete name of Id; callers that still set it must keep working
        var userId = parameters.Id != Guid.Empty ? parameters.Id : parameters.UserId;
#pragma warning restore CS0618

        var user = userId == Guid.Empty ? null : await userManager.FindByIdAsync(userId.ToString());
        if (user != null)
        {
            logger.LogInformation("Audit logging for user {UserName} Delete", user.Name);

            user.Deleted = true;
            user.DateUpdated = DateTime.UtcNow;

            // One write through the UserManager's own DbContext: stores the flag and replaces the security stamp,
            // which ends the deleted user's active sessions.
            var updateResult = await userManager.UpdateSecurityStampAsync(user);
            if (!updateResult.Succeeded)
            {
                foreach (var error in updateResult.Errors)
                {
                    handlerResult.Messages.Add(new ResultMessage(error.Description, ResultMessageType.Error));
                }

                return handlerResult;
            }

            cacheService.ClearCachedItemsWithPrefix(nameof(User));

            handlerResult.Entity = user;
            handlerResult.Success = true;
        }
        else
        {
            handlerResult.Messages.Add(new ResultMessage("User not found", ResultMessageType.Error));
        }

        return handlerResult;
    }

    /// <summary>
    /// Queries users with filtering, ordering and paging. Can use cache.
    /// </summary>
    /// <param name="parameters">Query options including roles and ids.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Paged list of users.</returns>
    #pragma warning disable CS1998
    public async Task<PaginatedList<User>> QueryUsersAsync(QueryUsersParameters parameters, CancellationToken cancellationToken = default)
    {
        using var scope = serviceScopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IZauberDbContext>();
        var query = BuildQuery(parameters, dbContext);
        if (parameters.Cached)
        {
            var cacheKey = $"{query.GenerateCacheKey<User>()}_Page{parameters.PageIndex}_Amount{parameters.AmountPerPage}";
            return (await cacheService.GetSetCachedItemAsync(cacheKey, async () => await query.ToPaginatedListAsync(parameters.PageIndex, parameters.AmountPerPage, cancellationToken)))!;
        }

        return await query.ToPaginatedListAsync(parameters.PageIndex, parameters.AmountPerPage, cancellationToken);
    }
    #pragma warning restore CS1998

    /// <summary>
    /// Gets a role and its users.
    /// </summary>
    /// <param name="parameters">Role id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Role or null.</returns>
    public async Task<Role?> GetRoleAsync(GetRoleParameters parameters, CancellationToken cancellationToken = default)
    {
        using var scope = serviceScopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IZauberDbContext>();
        
        IQueryable<Role> query = dbContext.Roles.AsNoTracking();
        if (parameters.IncludeUsers)
        {
            query = query.Include(x => x.UserRoles).ThenInclude(x => x.User).AsSplitQuery();
        }

        query = query.Where(x => x.Id == parameters.Id);

        return await query.FirstOrDefaultAsync(cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Creates or updates a role.
    /// </summary>
    /// <param name="parameters">Role to save.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result including success and messages.</returns>
    public async Task<HandlerResult<Role>> SaveRoleAsync(SaveRoleParameters parameters, CancellationToken cancellationToken = default)
    {
        using var scope = serviceScopeFactory.CreateScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<Role>>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var dbContext = scope.ServiceProvider.GetRequiredService<IZauberDbContext>();
        var handlerResult = new HandlerResult<Role>();

        if (parameters.Role != null)
        {
            var role = await roleManager.FindByIdAsync(parameters.Role.Id.ToString());
            var isUpdate = false;

            if (role == null)
            {
                role = parameters.Role;
                var result = await roleManager.CreateAsync(role);
                if (!result.Succeeded)
                {
                    handlerResult.Messages.AddRange(result.Errors.Select(e => new ResultMessage(e.Description, ResultMessageType.Error)));
                    return handlerResult;
                }
            }
            else
            {
                isUpdate = true;
                parameters.Role.MapTo(role);
                role.DateUpdated = DateTime.UtcNow;

                var result = await roleManager.UpdateAsync(role);
                if (!result.Succeeded)
                {
                    handlerResult.Messages.AddRange(result.Errors.Select(e => new ResultMessage(e.Description, ResultMessageType.Error)));
                    return handlerResult;
                }
            }

            // Note: Audit logging would need to be implemented without mediator.
            // Cannot use role.Id == Guid.Empty as the create/update marker because RoleManager.CreateAsync
            // assigns the Id before this log line runs — every save would log "Update".
            logger.LogInformation("Audit logging for role {RoleName} {Action}", role.Name, isUpdate ? "Update" : "Create");

            handlerResult.Entity = role;
            handlerResult.Success = true;
        }
        else
        {
            handlerResult.Messages.Add(new ResultMessage("Role is null", ResultMessageType.Error));
        }

        if (handlerResult.Success)
        {
            // Cached front-end pages hold their roles (and the role names the access check compares)
            cacheService.ClearCachedItemsWithPrefix(nameof(Content.Models.Content));
        }

        return handlerResult;
    }

    /// <summary>
    /// Deletes a role.
    /// </summary>
    /// <param name="parameters">Parameters containing the role id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result including success and messages.</returns>
    public async Task<HandlerResult<Role>> DeleteRoleAsync(DeleteRoleParameters parameters, CancellationToken cancellationToken = default)
    {
        using var scope = serviceScopeFactory.CreateScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<Role>>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var handlerResult = new HandlerResult<Role>();

        var role = await roleManager.FindByIdAsync(parameters.Id.ToString());
        if (role != null)
        {
            // Note: Audit logging would need to be implemented without mediator
            logger.LogInformation("Audit logging for role {RoleName} Delete", role.Name);
            
            var result = await roleManager.DeleteAsync(role);
            if (!result.Succeeded)
            {
                handlerResult.Messages.AddRange(result.Errors.Select(e => new ResultMessage(e.Description, ResultMessageType.Error)));
                return handlerResult;
            }

            handlerResult.Entity = role;
            handlerResult.Success = true;
        }
        else
        {
            handlerResult.Messages.Add(new ResultMessage("Role not found", ResultMessageType.Error));
        }

        if (handlerResult.Success)
        {
            // Cached front-end pages hold their roles (and the role names the access check compares)
            cacheService.ClearCachedItemsWithPrefix(nameof(Content.Models.Content));
        }

        return handlerResult;
    }

    /// <summary>
    /// Queries roles with filtering, ordering and paging.
    /// </summary>
    /// <param name="parameters">Query options including search and ids.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Paged list of roles.</returns>
    public async Task<PaginatedList<Role>> QueryRolesAsync(QueryRolesParameters parameters, CancellationToken cancellationToken = default)
    {
        using var scope = serviceScopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IZauberDbContext>();
        
        // Members only when asked for (ZA-056)
        var query = parameters.IncludeUsers
            ? dbContext.Roles.Include(x => x.UserRoles).ThenInclude(x => x.User).AsSplitQuery()
            : dbContext.Roles.AsQueryable();

        if (parameters.Query != null)
        {
            query = parameters.Query.Invoke();
        }
        else
        {
            if (parameters.AsNoTracking)
            {
                query = query.AsNoTracking();
            }

            if (!string.IsNullOrWhiteSpace(parameters.SearchTerm))
            {
                // Lowered without the server's culture (ZA-017); LOWER() in SQL on the column
                var searchTermLower = parameters.SearchTerm.ToLowerInvariant();
                query = query.Where(x => x.Name != null && x.Name.ToLower().Contains(searchTermLower));
            }

            if (parameters.Ids.Count != 0)
            {
                query = query.Where(x => parameters.Ids.Contains(x.Id));
                parameters.AmountPerPage = parameters.Ids.Count;
            }
        }

        if (parameters.WhereClause != null)
        {
            query = query.Where(parameters.WhereClause);
        }

        query = parameters.OrderBy switch
        {
            GetRolesOrderBy.Name => query.OrderBy(p => p.Name),
            GetRolesOrderBy.NameDescending => query.OrderByDescending(p => p.Name),
            GetRolesOrderBy.DateCreated => query.OrderBy(p => p.DateCreated),
            GetRolesOrderBy.DateCreatedDescending => query.OrderByDescending(p => p.DateCreated),
            _ => query.OrderByDescending(p => p.DateCreated)
        };

        return await query.ToPaginatedListAsync(parameters.PageIndex, parameters.AmountPerPage, cancellationToken);
    }

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
                    var userPrincipal = await signInManager.CreateUserPrincipalAsync(user);
                    if (parameters.ReturnUrl.IsNullOrWhiteSpace() && userPrincipal.IsInRole(Constants.Roles.AdminRoleName))
                    {
                        parameters.ReturnUrl = Urls.AdminBaseUrl;
                    }
                    loginResult.NavigateToUrl = parameters.ReturnUrl;
                }
                else
                {
                    if (signInResult.IsNotAllowed)
                    {
                        if (!await userManager.IsEmailConfirmedAsync(user))
                        {
                            loginResult.AddMessage("Email isn't confirmed. Check your inbox for a confirmation email", ResultMessageType.Warning);

                            // Resend confirmation email
                            var sendConfirmationEmailCommand = new SendEmailConfirmationParameters
                            {
                                ReturnUrl = parameters.ReturnUrl,
                                User = user
                            };
                            await emailService.SendEmailConfirmationAsync(sendConfirmationEmailCommand, cancellationToken);
                        }
                    }
                    else if (signInResult.IsLockedOut)
                    {
                        logger.LogWarning("User {RequestEmail} account is locked out", parameters.Email);
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
           logger.LogError(e, "Error during login for {Email}", parameters.Email);
           loginResult.AddMessage("An unexpected error occurred. Please try again.", ResultMessageType.Error);
           loginResult.Success = false;
           return loginResult;
        }

        return loginResult;
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
                    
                        if (parameters.ReturnUrl.IsNullOrWhiteSpace() && await userManager.IsInRoleAsync(user!, Constants.Roles.AdminRoleName))
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
            logger.LogError(e, "Error during registration for {Email}", parameters.Email);
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
            else if (!(await dataService.GetGlobalSettings()).AllowRegistration)
            {
                // A first sign-in through a provider is a registration, and registration is turned off
                loginResult.AddMessage("This site is not accepting new accounts", ResultMessageType.Error);
            }
            else
            {
                // User doesn't have an account, create one
                var user = new User
                {
                    UserName = info.Principal.FindFirstValue(ClaimTypes.Email),
                    Email = info.Principal.FindFirstValue(ClaimTypes.Email),
                    Name = info.Principal.FindFirstValue(ClaimTypes.Name),
                    DateCreated = DateTime.UtcNow,
                    DateUpdated = DateTime.UtcNow
                };

                var result = await userManager.CreateAsync(user);
                if (result.Succeeded)
                {
                    result = await userManager.AddLoginAsync(user, info);
                    if (result.Succeeded)
                    {
                        await signInManager.SignInAsync(user, isPersistent: false);
                        loginResult.Success = true;
                        loginResult.NavigateToUrl = parameters.ReturnUrl;
                    }
                    else
                    {
                        loginResult.Messages.AddRange(result.Errors.Select(e => new ResultMessage(e.Description, ResultMessageType.Error)));
                    }
                }
                else
                {
                    loginResult.Messages.AddRange(result.Errors.Select(e => new ResultMessage(e.Description, ResultMessageType.Error)));
                }
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

                // The token was issued for this exact address, so a different address in the link is refused.
                result = await userManager.ChangeEmailAsync(user, parameters.NewEmailAddress!, token);
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
            logger.LogError(e, "ForgotPasswordAsync failed for {Email}", parameters.Email);
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
            logger.LogError(e, "Error resetting password for {Email}", parameters.Email);
            resetPasswordResult.AddMessage("An unexpected error occurred. Please try again.", ResultMessageType.Error);
            resetPasswordResult.Success = false;
        }

        return resetPasswordResult;
    }

    /// <summary>
    /// Returns the currently authenticated user.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>User or null.</returns>
    public async Task<User?> GetCurrentUserAsync(CancellationToken cancellationToken = default)
    {
        return await currentUserAccessor.GetUserAsync(cancellationToken);
    }

    private static IQueryable<User> BuildQuery(GetUserParameters parameters, IZauberDbContext dbContext)
    {
        var query = dbContext.Users
            .Include(x => x.UserRoles)
            .ThenInclude(x => x.Role)
            .Include(x => x.PropertyData)
            .AsNoTracking()
            .AsSplitQuery()
            .Where(x => x.Id == parameters.Id && !x.Deleted);

        return query;
    }

    private static IQueryable<User> BuildQuery(QueryUsersParameters parameters, IZauberDbContext dbContext)
    {
        var query = dbContext.Users.Include(x => x.UserRoles).AsQueryable();

        if (parameters.Query != null)
        {
            query = parameters.Query.Invoke();
        }
        else
        {
            if (parameters.AsNoTracking)
            {
                query = query.AsNoTracking();
            }

            if (!parameters.IncludeDeleted)
            {
                query = query.Where(x => !x.Deleted);
            }

            if (!string.IsNullOrWhiteSpace(parameters.SearchTerm))
            {
                // Lowered without the server's culture (ZA-017); LOWER() in SQL on the column
                var searchTermLower = parameters.SearchTerm.ToLowerInvariant();
                query = query.Where(x => x.UserName != null && x.UserName.ToLower().Contains(searchTermLower));
            }

            if (parameters.Roles.Count != 0)
            {
                query = query.Where(x => x.UserRoles.Any(ur => ur.Role.Name != null && parameters.Roles.Contains(ur.Role.Name)));
            }

            if (parameters.Ids.Count != 0)
            {
                query = query.Where(x => parameters.Ids.Contains(x.Id));
                parameters.AmountPerPage = parameters.Ids.Count;
            }
        }

        if (parameters.WhereClause != null)
        {
            query = query.Where(parameters.WhereClause);
        }

        query = parameters.OrderBy switch
        {
            GetUsersOrderBy.DateUpdated => query.OrderBy(p => p.DateUpdated),
            GetUsersOrderBy.DateUpdatedDescending => query.OrderByDescending(p => p.DateUpdated),
            GetUsersOrderBy.DateCreated => query.OrderBy(p => p.DateCreated),
            GetUsersOrderBy.DateCreatedDescending => query.OrderByDescending(p => p.DateCreated),
            _ => query.OrderByDescending(p => p.DateCreated)
        };

        return query;
    }

    private async Task<HandlerResult<User>> UpdateUserPropertyValues(IZauberDbContext dbContext, User requestUser, HandlerResult<User> handlerResult, CancellationToken cancellationToken)
    {
        var user = await dbContext.Users.Include(x => x.PropertyData).FirstOrDefaultAsync(x => x.Id == requestUser.Id, cancellationToken);

        // Remove deleted items
        var deletedItems = user!.PropertyData.Where(epv => requestUser.PropertyData.All(npv => npv.Id != epv.Id)).ToList();
        foreach (var deletedItem in deletedItems)
        {
            dbContext.UserPropertyValues.Remove(deletedItem);
        }

        // Add or update items
        foreach (var newPropertyValue in requestUser.PropertyData)
        {
            var existingPropertyValue = user!.PropertyData.FirstOrDefault(epv => epv.Id == newPropertyValue.Id);
            if (existingPropertyValue == null)
            {
                // New property value
                dbContext.UserPropertyValues.Add(newPropertyValue);
            }
            else
            {
                // Existing property value, update its properties
                newPropertyValue.MapTo(existingPropertyValue);
            }
        }
        
        return await dbContext.SaveChangesAndLog(user, handlerResult, cacheService, extensionManager, cancellationToken);
    }
}
