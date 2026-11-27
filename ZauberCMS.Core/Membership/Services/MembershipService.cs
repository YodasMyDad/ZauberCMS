using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ZauberCMS.Core.Data;
using ZauberCMS.Core.Data.Interfaces;
using ZauberCMS.Core.Email.Interfaces;
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

public partial class MembershipService(
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
    /// How often signing in to an unconfirmed account sends the confirmation email again (ZA-106): at most once in
    /// this time for each account, so the sign-in page cannot be used to flood someone's inbox.
    /// </summary>
    public static readonly TimeSpan ConfirmationResendInterval = TimeSpan.FromMinutes(10);

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

                // The profile fields only: the sign-in and security fields change through UserManager (ZA-117)
                parameters.User.MapProfileTo(user);
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

                // The profile fields only: the sign-in and security fields change through UserManager (ZA-117)
                parameters.User.MapProfileTo(user);
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
    /// Deletes a user account and erases the person: see <see cref="IMembershipService.DeleteUserAsync"/>.
    /// </summary>
    /// <param name="parameters">Parameters containing the user id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result including success and messages.</returns>
    public async Task<HandlerResult<User>> DeleteUserAsync(DeleteUserParameters parameters, CancellationToken cancellationToken = default)
    {
        using var scope = serviceScopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IZauberDbContext>();
        var handlerResult = new HandlerResult<User>();

#pragma warning disable CS0618 // UserId is the obsolete name of Id; callers that still set it must keep working
        var userId = parameters.Id != Guid.Empty ? parameters.Id : parameters.UserId;
#pragma warning restore CS0618

        var user = userId == Guid.Empty
            ? null
            : await dbContext.Users.FirstOrDefaultAsync(x => x.Id == userId && !x.Deleted, cancellationToken);
        if (user == null)
        {
            handlerResult.Messages.Add(new ResultMessage("User not found", ResultMessageType.Error));
            return handlerResult;
        }

        // Deleting cannot be undone (D35), so it must not lock everybody out: not your own account, and not the last
        // administrator (the installer never reopens, and an erased user cannot be recovered)
        var deletedBy = await currentUserAccessor.GetUserAsync(cancellationToken);
        if (deletedBy?.Id == user.Id)
        {
            handlerResult.Messages.Add(new ResultMessage("You cannot delete your own account", ResultMessageType.Error));
            return handlerResult;
        }

        var adminUserIds = dbContext.UserRoles
            .Where(ur => ur.Role.Name == Constants.Roles.AdminRoleName && !ur.User.Deleted)
            .Select(ur => ur.UserId);
        if (await adminUserIds.ContainsAsync(user.Id, cancellationToken) &&
            !await adminUserIds.AnyAsync(id => id != user.Id, cancellationToken))
        {
            handlerResult.Messages.Add(new ResultMessage(
                "This is the last administrator. Make another user an administrator first", ResultMessageType.Error));
            return handlerResult;
        }

        // Deleting erases the person (ZA-033, Roadmap D35): the row stays, marked deleted, with nothing about them
        // in it, and the new security stamp ends their sessions. One save, through the save hooks.
        await UserAnonymiser.StageAsync(dbContext, user, cancellationToken);
        var saved = await dbContext.SaveChangesAndLog(user, handlerResult, cacheService, extensionManager, cancellationToken);
        if (saved.Success)
        {
            // Only once the erasure is stored: a refused save must not leave a "Deleted" audit row behind
            await deletedBy.AddAudit(user, user.UserName, AuditExtensions.AuditAction.Delete,
                scope.ServiceProvider.GetRequiredService<Audit.Interfaces.IAuditService>(), cancellationToken);
            logger.LogInformation("User {UserId} deleted and erased", user.Id);
        }

        return saved;
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

    private static readonly Lock ResendLock = new();

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
#pragma warning disable CA1304, CA1311, CA1862 // EF translates ToLower() on the column to SQL LOWER(); the culture overloads are not translatable
                query = query.Where(x => x.UserName != null && x.UserName.ToLower().Contains(searchTermLower));
#pragma warning restore CA1304, CA1311, CA1862
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
