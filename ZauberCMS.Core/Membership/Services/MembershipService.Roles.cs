using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ZauberCMS.Core.Data;
using ZauberCMS.Core.Email.Interfaces;
using ZauberCMS.Core.Email.Parameters;
using ZauberCMS.Core.Email.Services;
using ZauberCMS.Core.Extensions;
using ZauberCMS.Core.Membership.Models;
using ZauberCMS.Core.Membership.Parameters;
using ZauberCMS.Core.Membership.Mapping;
using ZauberCMS.Core.Providers;
using ZauberCMS.Core.Settings;
using ZauberCMS.Core.Shared.Models;

namespace ZauberCMS.Core.Membership.Services;

// Roles: get, save, delete and query.
public partial class MembershipService
{
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
#pragma warning disable CA1304, CA1311, CA1862 // EF translates ToLower() on the column to SQL LOWER(); the culture overloads are not translatable
                query = query.Where(x => x.Name != null && x.Name.ToLower().Contains(searchTermLower));
#pragma warning restore CA1304, CA1311, CA1862
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
}
