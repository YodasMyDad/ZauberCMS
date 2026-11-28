using System.Linq.Dynamic.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ZauberCMS.Core.Content.Interfaces;
using ZauberCMS.Core.Content.Parameters;
using ZauberCMS.Core.Data;
using ZauberCMS.Core.Extensions;
using ZauberCMS.Core.Languages.Interfaces;
using ZauberCMS.Core.Localization;
using ZauberCMS.Core.Plugins;
using ZauberCMS.Core.Settings;
using ZauberCMS.Core.Shared.Models;
using ZauberCMS.Core.Shared.Services;
using ZauberCMS.Core.Membership.Interfaces;
using ZauberCMS.Core.Membership.Models;
using ZauberCMS.Core.Shared;

namespace ZauberCMS.Core.Content.Services;

public partial class ContentService(
    IServiceScopeFactory serviceScopeFactory,
    ICacheService cacheService,
    IOptions<ZauberSettings> settings,
    ICurrentUserAccessor currentUserAccessor,
    ExtensionManager extensionManager,
    AppState appState,
    ILanguageService languageService,
    ILogger<ContentService> logger,
    IStringLocalizer<ZauberText> text)
    : IContentService
{
    private const string BlockListEditorComponentAlias = BlockListNestedContentReferenceProvider.BlockListEditorAlias;

    /// <summary>
    /// Retrieves a single content item based on the provided parameters. Can optionally use cache.
    /// </summary>
    /// <param name="parameters">Query options such as id, type, includes and caching.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The matching content item or null.</returns>
    public async Task<Models.Content?> GetContentAsync(GetContentParameters parameters,
        CancellationToken cancellationToken = default)
    {
        using var scope = serviceScopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IZauberDbContext>();
        var query = BuildQuery(parameters, dbContext);
        if (parameters.Cached)
        {
            // The key renders the SQL: only worth it when the result is cached (ZA-062)
            var cacheKey = query.GenerateCacheKey();
            return await cacheService.GetSetCachedItemAsync(cacheKey,
                async () => await FetchContentAsync(query, cancellationToken));
        }

        return await FetchContentAsync(query, cancellationToken);
    }

    /// <summary>
    /// Queries content items with filtering, paging and ordering. Can optionally use cache.
    /// </summary>
    /// <param name="parameters">Query options including filters, includes and paging.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Paged list of content items.</returns>
    public async Task<PaginatedList<Models.Content>> QueryContentAsync(QueryContentParameters parameters,
        CancellationToken cancellationToken = default)
    {
        using var scope = serviceScopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IZauberDbContext>();
        var query = BuildQuery(parameters, dbContext);
        if (parameters.Cached)
        {
            var cacheKey = $"{query.GenerateCacheKey()}_Page{parameters.PageIndex}_Amount{parameters.AmountPerPage}";
            return (await cacheService.GetSetCachedItemAsync(cacheKey,
                async () => await FetchContentAsync(query, parameters, cancellationToken)))!;
        }

        return await FetchContentAsync(query, parameters, cancellationToken);
    }

    /// <summary>
    /// Checks whether any content exists.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if any content exists.</returns>
    public async Task<bool> AnyContentAsync(CancellationToken cancellationToken = default)
    {
        // Asked on every front-end 404: cached under the Content prefix, which every content save and delete
        // clears (ZA-064). Content in the recycle bin doesn't count.
        return await cacheService.GetSetCachedItemAsync("AnyContent".GenerateCacheKey<Models.Content>(), async () =>
        {
            using var scope = serviceScopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<IZauberDbContext>();
            return await dbContext.Contents.AsNoTracking().AnyAsync(x => !x.Deleted, cancellationToken);
        });
    }

    /// <summary>
    /// Checks whether a content item has child items.
    /// </summary>
    /// <param name="parameters">Parent id and caching flag.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True when child content exists.</returns>
    public async Task<bool> HasChildContentAsync(HasChildContentParameters parameters,
        CancellationToken cancellationToken = default)
    {
        using var scope = serviceScopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IZauberDbContext>();
        if (parameters.Cached)
        {
            var cacheKey = parameters.GenerateCacheKey<Models.Content>("HasChild");
            return await cacheService.GetSetCachedItemAsync(cacheKey,
                async () => await dbContext.Contents.AsNoTracking().AnyAsync(c => c.ParentId == parameters.ParentId && !c.Deleted,
                    cancellationToken: cancellationToken));
        }

        return await dbContext.Contents.AsNoTracking()
            .AnyAsync(c => c.ParentId == parameters.ParentId && !c.Deleted, cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Returns content for a data grid with server-side filtering, ordering and paging.
    /// </summary>
    /// <param name="parameters">Grid options including filter, order and paging.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Data grid result with total count and items.</returns>
    public async Task<DataGridResult<Models.Content>> GetDataGridContentAsync(DataGridContentParameters parameters,
        CancellationToken cancellationToken = default)
    {
        using var scope = serviceScopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IZauberDbContext>();
        var result = new DataGridResult<Models.Content>();
        var query = dbContext.Contents
            .Include(x => x.ContentType)
            .Include(x => x.LastUpdatedBy)
            .Where(x => x.Deleted == false && !x.IsNestedContent)
            .AsQueryable();

        if (parameters.IncludeChildren)
        {
            query = query.Include(x => x.Children.Where(c => !c.Deleted)).AsSplitQuery();
        }

        if (parameters.AsNoTracking)
        {
            query = query.AsNoTracking();
        }

        if (!parameters.ContentTypeAlias.IsNullOrWhiteSpace())
        {
            var contentType = await dbContext.ContentTypes.AsNoTracking()
                .FirstOrDefaultAsync(x => x.Alias == parameters.ContentTypeAlias, cancellationToken);
            if (contentType != null)
            {
                parameters.ContentTypeId = contentType.Id;
            }
        }

        if (parameters.LastEditedBy != null)
        {
            query = query.Where(x => x.LastUpdatedById == parameters.LastEditedBy.Value);
        }

        if (parameters.ContentTypeId != null)
        {
            query = query.Where(x => x.ContentTypeId == parameters.ContentTypeId);
        }

        if (parameters.ParentId != null)
        {
            query = query.Where(x => x.ParentId == parameters.ParentId);
        }

        // The strings are parsed into expressions; GridQueryGuard refuses anything but ordinary column use.
        query = GridQueryGuard.Filter(query, parameters.Filter);

        if (!string.IsNullOrEmpty(parameters.Order))
        {
            query = GridQueryGuard.OrderBy(query, parameters.Order);
        }
        else
        {
            query = parameters.OrderBy switch
            {
                GetContentsOrderBy.DateUpdated => query.OrderBy(p => p.DateUpdated),
                GetContentsOrderBy.DateUpdatedDescending => query.OrderByDescending(p => p.DateUpdated),
                GetContentsOrderBy.DateCreated => query.OrderBy(p => p.DateCreated),
                GetContentsOrderBy.DateCreatedDescending => query.OrderByDescending(p => p.DateCreated),
                GetContentsOrderBy.SortOrder => query.OrderBy(p => p.SortOrder),
                _ => query.OrderByDescending(p => p.DateUpdated)
            };
        }

        result.Count = await query.CountAsync(cancellationToken);
        result.Items = await query.Skip(parameters.Skip).Take(parameters.Take)
            .ToListAsync(cancellationToken: cancellationToken);
        return result;
    }

    private static IQueryable<Models.Content> BuildQuery(GetContentParameters request, IZauberDbContext dbContext)
    {
        var query = dbContext.Contents
            .Include(x => x.ContentType)
            .Include(x => x.PropertyData)
            .AsSplitQuery()
            .AsQueryable();

        if (request.AsNoTracking)
        {
            query = query.AsNoTracking();
        }

        if (!request.IncludeUnpublished)
        {
            query = query.Where(x => x.Published);
        }

        if (!request.IncludeDeleted)
        {
            query = query.Where(x => !x.Deleted);
        }

        if (request.IncludeUnpublishedContent)
        {
            query = query.Include(x => x.UnpublishedContent);
        }

        if (request.IncludeParent)
        {
            query = query.Include(x => x.Parent);
        }

        if (request.IncludeChildren)
        {
            // Both Published and Deleted filters apply to included children — soft-deleted
            // children must not bleed into the navigation tree under any caller.
            query = request.IncludeUnpublished
                ? query.Include(x => x.Children.Where(c => !c.Deleted))
                : query.Include(x => x.Children.Where(c => c.Published && !c.Deleted));
            query = query.AsSplitQuery();
        }

        if (request.IncludeContentRoles)
        {
            query = query.Include(x => x.ContentRoles).ThenInclude(x => x.Role).AsSplitQuery();
        }

        if (request.Id != null)
        {
            return query.Where(x => x.Id == request.Id);
        }

        if (!request.ContentTypeAlias.IsNullOrWhiteSpace())
        {
            return query.Where(x => x.ContentType != null && x.ContentType.Alias == request.ContentTypeAlias);
        }

        return query;
    }

    private static IQueryable<Models.Content> BuildQuery(QueryContentParameters request, IZauberDbContext dbContext)
    {
        var query = dbContext.Contents.Include(x => x.ContentType)
            .Include(x => x.PropertyData).AsSplitQuery().AsQueryable();

        if (request.Query != null)
        {
            query = request.Query.Invoke();
        }
        else
        {
            if (request.IncludeContentRoles)
            {
                query = query.Include(x => x.ContentRoles);
            }

            if (request.OnlyUnpublished)
            {
                query = query.Include(x => x.UnpublishedContent);
                query = query.Where(x => x.UnpublishedContentId != null || x.Published == false);
            }
            else
            {
                query = !request.IncludeUnpublished
                    ? query.Where(x => x.Published)
                    : query.Include(x => x.UnpublishedContent);
            }

            if (request.OnlyDeleted)
            {
                query = query.Where(x => x.Deleted);
            }
            else if (!request.IncludeDeleted)
            {
                query = query.Where(x => !x.Deleted);
            }

            if (request.IncludeChildren)
            {
                // Recycled children are never included, whatever the deleted flags ask of the items themselves (ZA-065)
                query = request.IncludeUnpublished
                    ? query.Include(x => x.Children.Where(c => !c.Deleted))
                        .ThenInclude(x => x.UnpublishedContent)
                    : query.Include(x => x.Children.Where(c => c.Published && !c.Deleted));
            }

            if (request.RootContentOnly)
            {
                query = query.Where(x => x.ParentId == null);
            }

            if (request.TagSlugs.Count != 0)
            {
                query = (from content in query
                        join tagItem in dbContext.TagItems on content.Id equals tagItem.ItemId
                        join tag in dbContext.Tags on tagItem.TagId equals tag.Id
                        where request.TagSlugs.Contains(tag.Slug)
                        select content)
                    .Distinct();
            }

            if (request.AsNoTracking)
            {
                query = query.AsNoTracking();
            }

            if (!string.IsNullOrWhiteSpace(request.SearchTerm))
            {
                // LOWER() in SQL on the column, the term lowered here without the server's culture: a culture
                // such as Turkish lowers "I" to "ı" and the search then finds nothing (ZA-017)
                var searchTerm = request.SearchTerm.ToLowerInvariant();
#pragma warning disable CA1304, CA1311, CA1862 // EF translates ToLower() on the column to SQL LOWER(); the culture overloads are not translatable
                query = query.Where(x => x.Name != null && x.Name.ToLower().Contains(searchTerm));
#pragma warning restore CA1304, CA1311, CA1862
            }

            if (!string.IsNullOrWhiteSpace(request.ContentTypeAlias))
            {
                // Filtered in SQL: looking the type up first was an extra, uncached query before the cache check
                // on every call (ZA-062). The alias wins over ContentTypeId, as before.
                var contentTypeAlias = request.ContentTypeAlias;
                query = query.Where(x => x.ContentType != null && x.ContentType.Alias == contentTypeAlias);
            }
            else if (request.ContentTypeId != null)
            {
                query = query.Where(x => x.ContentTypeId == request.ContentTypeId);
            }

            if (request.ParentId != null)
            {
                query = query.Where(x => x.ParentId == request.ParentId);
            }

            var idCount = request.Ids.Count;
            if (idCount != 0)
            {
                query = query.Where(x => request.Ids.Contains(x.Id));
                request.AmountPerPage = idCount;
            }

            query = ApplyNestedFilter(query, request.NestedFilter);
        }

        if (request.WhereClause != null)
        {
            query = query.Where(request.WhereClause);
        }

        query = request.OrderBy switch
        {
            GetContentsOrderBy.DateUpdated => query.OrderBy(p => p.DateUpdated),
            GetContentsOrderBy.DateUpdatedDescending => query.OrderByDescending(p => p.DateUpdated),
            GetContentsOrderBy.DateCreated => query.OrderBy(p => p.DateCreated),
            GetContentsOrderBy.DateCreatedDescending => query.OrderByDescending(p => p.DateCreated),
            GetContentsOrderBy.SortOrder => query.OrderBy(p => p.SortOrder),
            _ => query.OrderByDescending(p => p.DateUpdated)
        };

        // Pages with the same date or sort order keep their place from one page of results to the next (ZA-115)
        return ((IOrderedQueryable<Models.Content>)query).ThenBy(p => p.Id);
    }

    private static async Task<PaginatedList<Models.Content>> FetchContentAsync(IQueryable<Models.Content> query,
        QueryContentParameters request, CancellationToken cancellationToken)
    {
        return await query.ToPaginatedListAsync(request.PageIndex, request.AmountPerPage, cancellationToken);
    }

    private static async Task<Models.Content?> FetchContentAsync(IQueryable<Models.Content> query,
        CancellationToken cancellationToken)
    {
        return await query.FirstOrDefaultAsync(cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Stages the audit row of a change; the change's own save writes it, so a vetoed or failed change leaves none,
    /// and the save hooks still see the change's rows as added, modified or deleted (ZA-012). Nobody signed in, no row.
    /// </summary>
    private static void AddAuditIfUser(IZauberDbContext dbContext, User? user, string? name, string action)
    {
        if (user == null) return;
        dbContext.Audits.Add(new ZauberCMS.Core.Audit.Models.Audit
        {
            Description = $"{user.Name} {action} {name ?? string.Empty}"
        });
    }

    private static IQueryable<Models.Content> ApplyNestedFilter(IQueryable<Models.Content> query,
        BaseQueryContentParameters.NestedContentFilter filter)
    {
        return filter switch
        {
            BaseQueryContentParameters.NestedContentFilter.Exclude => query.Where(c => !c.IsNestedContent),
            BaseQueryContentParameters.NestedContentFilter.Only => query.Where(c => c.IsNestedContent),
            _ => query // Include does nothing
        };
    }
}
