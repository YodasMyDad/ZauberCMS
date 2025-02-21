using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ZauberCMS.Core.Data;
using ZauberCMS.Core.Extensions;
using ZauberCMS.Core.Shared.Models;
using ZauberCMS.Core.Shared.Services;
using ZauberCMS.Core.Tags.Commands;
using ZauberCMS.Core.Tags.Models;

namespace ZauberCMS.Core.Tags.Handlers;

public class QueryTagHandler(IServiceProvider serviceProvider, ICacheService cacheService)
    : IRequestHandler<QueryTagCommand, PaginatedList<Tag>>
{
    public async Task<PaginatedList<Tag>> Handle(QueryTagCommand request, CancellationToken cancellationToken)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ZauberDbContext>();
        var query = BuildQuery(request, dbContext);
        var cacheKey = query.GenerateCacheKey(typeof(Tag));

        if (request.Cached)
        {
            return (await cacheService.GetSetCachedItemAsync(cacheKey, async () => await FetchTagsAsync(request, dbContext, cancellationToken)))!;
        }

        return await FetchTagsAsync(request, dbContext, cancellationToken);
    }

    private static IQueryable<Tag> BuildQuery(QueryTagCommand request, ZauberDbContext dbContext)
    {
        var query = dbContext.Tags.AsQueryable();

        if (request.Query != null)
        {
            query = request.Query;
        }
        else
        {
            if (request.AsNoTracking)
            {
                query = query.AsNoTracking();
            }

            if (request.Ids.Count != 0)
            {
                query = query.Where(x => request.Ids.Contains(x.Id));
            }

            if (request.TagNames.Count != 0)
            {
                query = query.Where(x => request.TagNames.Contains(x.TagName));
            }

            if (request.TagSlugs.Count != 0)
            {
                query = query.Where(x => request.TagSlugs.Contains(x.Slug));
            }

            if (request.ItemIds.Count != 0)
            {
                query = query.Include(x => x.TagItems)
                             .Where(x => x.TagItems.Any(ti => request.ItemIds.Contains(ti.ItemId)))
                             .AsSplitQuery();
            }
        }

        if (request.WhereClause != null)
        {
            query = query.Where(request.WhereClause);
        }

        query = request.OrderBy switch
        {
            GetTagOrderBy.DateCreated => query.OrderBy(p => p.DateCreated),
            GetTagOrderBy.DateCreatedDescending => query.OrderByDescending(p => p.DateCreated),
            GetTagOrderBy.TagName => query.OrderBy(p => p.TagName),
            GetTagOrderBy.TagNameDescending => query.OrderByDescending(p => p.TagName),
            _ => query.OrderBy(p => p.SortOrder)
        };

        return query;
    }

    private static Task<PaginatedList<Tag>> FetchTagsAsync(QueryTagCommand request, ZauberDbContext dbContext, CancellationToken cancellationToken)
    {
        var query = BuildQuery(request, dbContext);
        return Task.FromResult(query.ToPaginatedList(request.PageIndex, request.AmountPerPage));
    }
}
