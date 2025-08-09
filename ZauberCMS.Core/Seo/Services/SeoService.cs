using AutoMapper;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ZauberCMS.Core.Audit.Interfaces;
using ZauberCMS.Core.Data;
using ZauberCMS.Core.Extensions;
using ZauberCMS.Core.Membership.Models;
using ZauberCMS.Core.Plugins;
using ZauberCMS.Core.Seo.Interfaces;
using ZauberCMS.Core.Seo.Models;
using ZauberCMS.Core.Seo.Parameters;
using ZauberCMS.Core.Shared.Models;
using ZauberCMS.Core.Shared.Services;

namespace ZauberCMS.Core.Seo.Services;

public class SeoService(
    IServiceProvider serviceProvider,
    ICacheService cacheService,
    AuthenticationStateProvider authenticationStateProvider,
    ExtensionManager extensionManager)
    : ISeoService
{
    /// <summary>
    /// Creates or updates an SEO redirect. Logs audit and saves changes.
    /// </summary>
    /// <param name="parameters">Redirect to save.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result including success and messages.</returns>
    public async Task<HandlerResult<SeoRedirect>> SaveRedirectAsync(SaveRedirectParameters parameters, CancellationToken cancellationToken = default)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IZauberDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var authState = await authenticationStateProvider.GetAuthenticationStateAsync();
        var user = await userManager.GetUserAsync(authState.User);
        
        var handlerResult = new HandlerResult<SeoRedirect>();
        var isUpdate = false;
        if (parameters.Redirect != null)
        {
            // Get the DB version
            var redirect = dbContext.Redirects
                .FirstOrDefault(x => x.Id == parameters.Redirect.Id);

            if (redirect == null)
            {
                redirect = parameters.Redirect;
                dbContext.Redirects.Add(redirect);
            }
            else
            {
                isUpdate = true;
                // Note: We need to get IMapper from the service provider since it's not injected directly
                var mapper = scope.ServiceProvider.GetRequiredService<IMapper>();
                mapper.Map(parameters.Redirect, redirect);
                redirect.DateUpdated = DateTime.UtcNow;                
            }
            
            var auditService = scope.ServiceProvider.GetRequiredService<IAuditService>();
            await user.AddAuditWithService(redirect, redirect.FromUrl, isUpdate ? AuditExtensions.AuditAction.Update : AuditExtensions.AuditAction.Create, auditService, cancellationToken);
            return await dbContext.SaveChangesAndLog(redirect, handlerResult, cacheService, extensionManager, cancellationToken);
        }

        handlerResult.AddMessage("Redirect is null", ResultMessageType.Error);
        return handlerResult;
    }

    /// <summary>
    /// Queries redirects with filtering, ordering and amount limiting. Can use cache.
    /// </summary>
    /// <param name="parameters">Query options including ids and amount.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of redirects.</returns>
    public async Task<List<SeoRedirect>> QueryRedirectsAsync(QueryRedirectsParameters parameters, CancellationToken cancellationToken = default)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IZauberDbContext>();
        var query = BuildQuery(parameters, dbContext);
        var cacheKey = query.GenerateCacheKey(typeof(SeoRedirect));
        
        if (parameters.Cached)
        {
            return (await cacheService.GetSetCachedItemAsync(cacheKey, async () => await FetchContentAsync(parameters, dbContext, cancellationToken)))!;
        }

        return await FetchContentAsync(parameters, dbContext, cancellationToken);
    }

    /// <summary>
    /// Deletes a redirect by id. Logs audit.
    /// </summary>
    /// <param name="parameters">Redirect id to delete.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result including success and messages.</returns>
    public async Task<HandlerResult<SeoRedirect?>> DeleteRedirectAsync(DeleteRedirectParameters parameters, CancellationToken cancellationToken = default)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IZauberDbContext>();
        var authState = await authenticationStateProvider.GetAuthenticationStateAsync();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var user = await userManager.GetUserAsync(authState.User);
        var handlerResult = new HandlerResult<SeoRedirect>();

        SeoRedirect? redirect = null;
        if (parameters.Id != null)
        {
            redirect =
                await dbContext.Redirects.FirstOrDefaultAsync(l => l.Id == parameters.Id,
                    cancellationToken: cancellationToken);
            if (redirect != null)
            {
                var auditService = scope.ServiceProvider.GetRequiredService<IAuditService>();
                await user.AddAuditWithService(redirect, $"Redirect ({redirect.FromUrl} -> {redirect.ToUrl})",
                    AuditExtensions.AuditAction.Delete, auditService,
                    cancellationToken);
                dbContext.Redirects.Remove(redirect);
            }
        }

        return (await dbContext.SaveChangesAndLog(redirect, handlerResult, cacheService, extensionManager, cancellationToken))!;
    }

    private static IQueryable<SeoRedirect> BuildQuery(QueryRedirectsParameters parameters, IZauberDbContext dbContext)
    {
        var query = dbContext.Redirects.AsQueryable();

        if (parameters.Query != null)
        {
            query = parameters.Query.Invoke();
            return query;
        }

        if (parameters.AsNoTracking)
        {
            query = query.AsNoTracking();
        }
        
        if (parameters.Ids.Count > 0)
        {
            query = query.Where(p => parameters.Ids.Contains(p.Id));
        }
        
        query = parameters.OrderBy switch
        {
            GetSeoRedirectOrderBy.DateCreated => query.OrderBy(p => p.DateCreated),
            GetSeoRedirectOrderBy.DateCreatedDescending => query.OrderByDescending(p => p.DateCreated),
            GetSeoRedirectOrderBy.DateUpdated => query.OrderBy(p => p.DateUpdated),
            GetSeoRedirectOrderBy.DateUpdatedDescending => query.OrderByDescending(p => p.DateUpdated),
            _ => query.OrderByDescending(p => p.FromUrl)
        };

        return query.Take(parameters.Amount);
    }
    
    private static async Task<List<SeoRedirect>> FetchContentAsync(QueryRedirectsParameters parameters, IZauberDbContext dbContext, CancellationToken cancellationToken)
    {
        var query = BuildQuery(parameters, dbContext);
        return await query.ToListAsync(cancellationToken: cancellationToken);
    }
}
