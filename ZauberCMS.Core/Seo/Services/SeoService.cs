using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ZauberCMS.Core.Audit.Interfaces;
using ZauberCMS.Core.Data;
using ZauberCMS.Core.Extensions;
using ZauberCMS.Core.Membership.Interfaces;
using ZauberCMS.Core.Plugins;
using ZauberCMS.Core.Seo.Interfaces;
using ZauberCMS.Core.Seo.Models;
using ZauberCMS.Core.Seo.Parameters;
using ZauberCMS.Core.Seo.Mapping;
using ZauberCMS.Core.Shared.Models;
using ZauberCMS.Core.Shared.Services;

namespace ZauberCMS.Core.Seo.Services;

public class SeoService(
    IServiceScopeFactory serviceScopeFactory,
    ICacheService cacheService,
    ICurrentUserAccessor currentUserAccessor,
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
        using var scope = serviceScopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IZauberDbContext>();
        var user = await currentUserAccessor.GetUserAsync(cancellationToken);
        
        var handlerResult = new HandlerResult<SeoRedirect>();
        var isUpdate = false;
        if (parameters.Redirect != null)
        {
            if (!RedirectTarget.IsValid(parameters.Redirect.ToUrl))
            {
                handlerResult.AddMessage(
                    "The address to redirect to must be a path on this site starting with / or a full http(s) address",
                    ResultMessageType.Error);
                return handlerResult;
            }

            var fromUrlProblem = CheckFromUrl(parameters.Redirect);
            if (fromUrlProblem != null)
            {
                handlerResult.AddMessage(fromUrlProblem, ResultMessageType.Error);
                return handlerResult;
            }

            // A redirect that sends visitors round in circles is refused (ZA-116)
            var others = await dbContext.Redirects.AsNoTracking()
                .Where(x => x.Id != parameters.Redirect.Id)
                .ToListAsync(cancellationToken);
            var loop = RedirectRules.FindLoop(parameters.Redirect, others);
            if (loop != null)
            {
                handlerResult.AddMessage(loop, ResultMessageType.Error);
                return handlerResult;
            }

            // Get the DB version
            var redirect = await dbContext.Redirects
                .FirstOrDefaultAsync(x => x.Id == parameters.Redirect.Id, cancellationToken);

            if (redirect == null)
            {
                redirect = parameters.Redirect;
                dbContext.Redirects.Add(redirect);
            }
            else
            {
                isUpdate = true;
                parameters.Redirect.MapTo(redirect);
                redirect.DateUpdated = DateTime.UtcNow;                
            }
            
            var auditService = scope.ServiceProvider.GetRequiredService<IAuditService>();
            await user.AddAudit(redirect, redirect.FromUrl, isUpdate ? AuditExtensions.AuditAction.Update : AuditExtensions.AuditAction.Create, auditService, cancellationToken);
            // Clears the SeoRedirect cache prefix on every server, the redirect middleware's compiled list with it
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
        using var scope = serviceScopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IZauberDbContext>();
        var query = BuildQuery(parameters, dbContext);
        if (parameters.Cached)
        {
            var cacheKey = query.GenerateCacheKey<SeoRedirect>();
            return await cacheService.GetSetCachedItemAsync(cacheKey, async () => await query.ToListAsync(cancellationToken: cancellationToken)) ?? new List<SeoRedirect>();
        }

        return await query.ToListAsync(cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Deletes a redirect by id. Logs audit.
    /// </summary>
    /// <param name="parameters">Redirect id to delete.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result including success and messages.</returns>
    public async Task<HandlerResult<SeoRedirect?>> DeleteRedirectAsync(DeleteRedirectParameters parameters, CancellationToken cancellationToken = default)
    {
        using var scope = serviceScopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IZauberDbContext>();
        var user = await currentUserAccessor.GetUserAsync(cancellationToken);
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
                await user.AddAudit(redirect, $"Redirect ({redirect.FromUrl} -> {redirect.ToUrl})",
                    AuditExtensions.AuditAction.Delete, auditService,
                    cancellationToken);
                dbContext.Redirects.Remove(redirect);
            }
        }

        return (await dbContext.SaveChangesAndLog(redirect, handlerResult, cacheService, extensionManager, cancellationToken))!;
    }

    /// <summary>Why the From URL cannot be saved, or null: an exact one is a path, a regular expression must parse.</summary>
    private static string? CheckFromUrl(SeoRedirect redirect)
    {
        if (string.IsNullOrWhiteSpace(redirect.FromUrl))
        {
            return "The address to redirect from is empty";
        }

        if (!RedirectRules.IsRegex(redirect))
        {
            return redirect.FromUrl.StartsWith('/') ? null : "The address to redirect from must start with /";
        }

        try
        {
            RedirectRules.CreateRegex(redirect.FromUrl);
            return null;
        }
        catch (ArgumentException ex)
        {
            return $"The address to redirect from is not a valid regular expression: {ex.Message}";
        }
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

        // The same order every time: the first regular expression that matches wins (ZA-115)
        query = ((IOrderedQueryable<SeoRedirect>)query).ThenBy(p => p.Id);

        return query.Take(parameters.Amount);
    }
}
