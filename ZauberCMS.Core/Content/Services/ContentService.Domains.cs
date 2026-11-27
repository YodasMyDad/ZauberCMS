using System.Linq.Dynamic.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ZauberCMS.Core.Content.Interfaces;
using ZauberCMS.Core.Content.Models;
using ZauberCMS.Core.Content.Parameters;
using ZauberCMS.Core.Content.Mapping;
using ZauberCMS.Core.Data;
using ZauberCMS.Core.Extensions;
using ZauberCMS.Core.Plugins;
using ZauberCMS.Core.Settings;
using ZauberCMS.Core.Shared.Models;
using ZauberCMS.Core.Shared.Services;
using ZauberCMS.Core.Membership.Interfaces;
using ZauberCMS.Core.Membership.Models;
using ZauberCMS.Core.Shared;
using System.Text.Json;

namespace ZauberCMS.Core.Content.Services;

// Culture domains, and the language of each page the front-end lookup uses.
public partial class ContentService
{
    /// <summary>
    /// Retrieves a domain by url or id. Returns an empty domain when not found.
    /// </summary>
    /// <param name="parameters">Query options including url or id, and tracking flag.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A domain instance (empty if not found).</returns>
    public async Task<Domain> GetDomainAsync(GetDomainParameters parameters,
        CancellationToken cancellationToken = default)
    {
        using var scope = serviceScopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IZauberDbContext>();
        var query = dbContext.Domains.AsQueryable();
        if (parameters.AsNoTracking)
        {
            query = query.AsNoTracking();
        }

        if (parameters.Url != null)
        {
            return await query.FirstOrDefaultAsync(x => x.Url == parameters.Url,
                cancellationToken: cancellationToken) ?? new Domain();
        }

        if (parameters.Id != null)
        {
            return await query.FirstOrDefaultAsync(x => x.Id == parameters.Id, cancellationToken: cancellationToken) ??
                   new Domain();
        }

        return await query.FirstOrDefaultAsync(cancellationToken: cancellationToken) ?? new Domain();
    }

    /// <summary>
    /// Creates or updates a domain and logs audit entries.
    /// </summary>
    /// <param name="parameters">The domain to save.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result including success state and messages.</returns>
    public async Task<HandlerResult<Domain>> SaveDomainAsync(SaveDomainParameters parameters,
        CancellationToken cancellationToken = default)
    {
        using var scope = serviceScopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IZauberDbContext>();
        var user = await currentUserAccessor.GetUserAsync(cancellationToken);
        var handlerResult = new HandlerResult<Domain>();
        var isUpdate = false;

        if (parameters.Domain == null)
        {
            handlerResult.AddMessage("Domain is null", ResultMessageType.Error);
            return handlerResult;
        }

        var domain = await dbContext.Domains.FirstOrDefaultAsync(x => x.Id == parameters.Domain.Id, cancellationToken);
        if (domain == null)
        {
            domain = parameters.Domain;
            dbContext.Domains.Add(domain);
        }
        else
        {
            isUpdate = true;
            parameters.Domain.MapTo(domain);
            domain.DateUpdated = DateTime.UtcNow;
        }

        AddAuditIfUser(dbContext, user, $"Domain ({domain.Url})", isUpdate ? "Updated" : "Created");

        var saved = await dbContext.SaveChangesAndLog(domain, handlerResult, cacheService, extensionManager,
            cancellationToken);
        if (saved.Success)
        {
            // Cached front-end lookups (URL → page and language) depend on the domains
            cacheService.ClearCachedItemsWithPrefix(nameof(Models.Content));
        }

        return saved;
    }

    /// <summary>
    /// Queries domains with filtering and paging.
    /// </summary>
    /// <param name="parameters">Query options including ids, where clause and ordering.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Paged list of domains.</returns>
    public async Task<PaginatedList<Domain>> QueryDomainAsync(QueryDomainParameters parameters,
        CancellationToken cancellationToken = default)
    {
        using var scope = serviceScopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IZauberDbContext>();
        var query = dbContext.Domains.AsQueryable();
        
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

            var idCount = parameters.Ids.Count;
            if (idCount != 0)
            {
                query = query.Where(x => parameters.Ids.Contains(x.Id));
                parameters.AmountPerPage = idCount;
            }

            if (parameters.ContentId != null)
            {
                query = query.Where(x => x.ContentId == parameters.ContentId);
            }

            if (parameters.LanguageId != null)
            {
                query = query.Where(x => x.LanguageId == parameters.LanguageId);
            }

            if (parameters.WhereClause != null)
            {
                query = query.Where(parameters.WhereClause);
            }

            query = parameters.OrderBy switch
            {
                GetDomainOrderBy.DateCreated => query.OrderBy(p => p.DateCreated),
                GetDomainOrderBy.DateCreatedDescending => query.OrderByDescending(p => p.DateCreated),
                GetDomainOrderBy.Url => query.OrderBy(p => p.Url),
                _ => query.OrderByDescending(p => p.DateCreated)
            };    
        }
        
        return await query.ToPaginatedListAsync(parameters.PageIndex, parameters.AmountPerPage, cancellationToken);
    }

    /// <summary>
    /// Deletes a domain by id or by content id and logs an audit entry.
    /// </summary>
    /// <param name="parameters">Parameters to identify the domain.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result including success state and messages.</returns>
    public async Task<HandlerResult<Domain?>> DeleteDomainAsync(DeleteDomainParameters parameters,
        CancellationToken cancellationToken = default)
    {
        using var scope = serviceScopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IZauberDbContext>();
        var user = await currentUserAccessor.GetUserAsync(cancellationToken);
        var handlerResult = new HandlerResult<Domain?>();

        Domain? domain = null;
        if (parameters.Id != null)
        {
            domain = await dbContext.Domains.FirstOrDefaultAsync(x => x.Id == parameters.Id,
                cancellationToken: cancellationToken);
        }
        else if (parameters.ContentId != null)
        {
            domain = await dbContext.Domains.FirstOrDefaultAsync(x => x.ContentId == parameters.ContentId,
                cancellationToken: cancellationToken);
        }

        if (domain != null)
        {
            AddAuditIfUser(dbContext, user, $"Domain ({domain.Url})", "Deleted");

            dbContext.Domains.Remove(domain);
            var deleted = await dbContext.SaveChangesAndLog(domain, handlerResult, cacheService, extensionManager,
                cancellationToken);
            if (deleted.Success)
            {
                // Cached front-end lookups (URL → page and language) depend on the domains
                cacheService.ClearCachedItemsWithPrefix(nameof(Models.Content));
            }

            return deleted;
        }

        handlerResult.AddMessage("Unable to delete, as no Domain with that id exists", ResultMessageType.Warning);
        return handlerResult;
    }

    /// <summary>
    /// Returns a dictionary mapping content ids and urls to language ISO codes. Uses caching.
    /// </summary>
    /// <param name="parameters">Unused. Reserved for future options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Dictionary of content identifiers to language codes.</returns>
    public async Task<Dictionary<object, string>> GetContentLanguagesAsync(GetContentLanguagesParameters parameters,
        CancellationToken cancellationToken = default)
    {
        using var scope = serviceScopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IZauberDbContext>();
        var query = dbContext.Contents.AsNoTracking()
            .Where(x => !x.Deleted && !x.IsNestedContent)
            .Include(x => x.Language)
            .Select(c => new { c.Id, c.Url, c.Language })
            .Where(x => x.Language != null && x.Url != null);

        var cacheKey = query.GenerateCacheKey<Models.Content>();

        return (await cacheService.GetSetCachedItemAsync(cacheKey, async () =>
        {
            var contentLanguages = await query.ToListAsync(cancellationToken: cancellationToken);
            var dict = new Dictionary<object, string>();
            foreach (var c in contentLanguages)
            {
                // TryAdd: this runs on every front-end request, so one duplicate URL (older data, or two sites
                // with the same slug) must not take every page down (ZA-046). The first page with it wins.
                dict.TryAdd(c.Url!, c.Language!.LanguageIsoCode!);
                dict.TryAdd(c.Id, c.Language!.LanguageIsoCode!);
            }

            return dict;
        }))!;
    }

    /// <summary>
    /// Returns all domains with languages from cache.
    /// </summary>
    /// <param name="parameters">Unused. Reserved for future options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of domains.</returns>
    public async Task<List<Domain>> GetCachedDomainsAsync(CachedDomainsParameters parameters,
        CancellationToken cancellationToken = default)
    {
        // A fixed key under the Domain prefix, which every domain save and delete clears: this runs on each request
        // that needs the domains (redirects, content finders), so a cache hit builds no DbContext and renders no SQL
        // (ZA-175)
        return (await cacheService.GetSetCachedItemAsync(CachedDomainsKey, async () =>
        {
            using var scope = serviceScopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<IZauberDbContext>();
            return await dbContext.Domains.AsNoTracking().Include(x => x.Language)
                .ToListAsync(cancellationToken: cancellationToken);
        }))!;
    }

    /// <summary>The cache key of <see cref="GetCachedDomainsAsync"/>: under the prefix a domain save clears.</summary>
    internal static readonly string CachedDomainsKey = "CachedDomains".GenerateCacheKey<Domain>();
}
