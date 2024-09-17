using System.Security.Cryptography;
using System.Text;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ZauberCMS.Core.Data;
using ZauberCMS.Core.Extensions;
using ZauberCMS.Core.Media.Commands;
using ZauberCMS.Core.Shared.Models;
using ZauberCMS.Core.Shared.Services;

namespace ZauberCMS.Core.Media.Handlers;

public class QueryMediaHandler(IServiceProvider serviceProvider, ICacheService cacheService)
    : IRequestHandler<QueryMediaCommand, PaginatedList<Models.Media>>
{
    public async Task<PaginatedList<Models.Media>> Handle(QueryMediaCommand request, CancellationToken cancellationToken)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ZauberDbContext>();
        var cacheKey = GenerateCacheKey(request, dbContext);

        if (request.Cached)
        {
            return (await cacheService.GetSetCachedItemAsync(cacheKey, async () => await FetchMediaAsync(request, dbContext, cancellationToken)))!;
        }

        return await FetchMediaAsync(request, dbContext, cancellationToken);
    }

    private string GenerateCacheKey(QueryMediaCommand request, ZauberDbContext dbContext)
    {
        var query = BuildQuery(request, dbContext);
        var queryString = query.ToQueryString();
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(queryString));
        return typeof(Models.Media).ToCacheKey(Convert.ToBase64String(hash));
    }

    private IQueryable<Models.Media> BuildQuery(QueryMediaCommand request, ZauberDbContext dbContext)
    {
        var query = dbContext.Medias.Include(x => x.Parent).AsQueryable();

        if (request.Query != null)
        {
            query = request.Query;
        }
        else
        {
            if (request.IncludeChildren)
            {
                query = query.Include(x => x.Children).AsSplitQuery();
            }

            if (request.AsNoTracking)
            {
                query = query.AsNoTracking();
            }

            if (request.Ids.Count != 0)
            {
                query = query.Where(x => request.Ids.Contains(x.Id));
                request.AmountPerPage = request.Ids.Count;
            }

            if (request.MediaTypes.Count != 0)
            {
                query = query.Where(x => request.MediaTypes.Contains(x.MediaType));
            }
        }

        if (request.WhereClause != null)
        {
            query = query.Where(request.WhereClause);
        }

        query = request.OrderBy switch
        {
            GetMediaOrderBy.DateUpdated => query.OrderBy(p => p.DateUpdated),
            GetMediaOrderBy.DateUpdatedDescending => query.OrderByDescending(p => p.DateUpdated),
            GetMediaOrderBy.DateCreated => query.OrderBy(p => p.DateCreated),
            GetMediaOrderBy.DateCreatedDescending => query.OrderByDescending(p => p.DateCreated),
            GetMediaOrderBy.Name => query.OrderBy(p => p.Name),
            GetMediaOrderBy.NameDescending => query.OrderByDescending(p => p.Name),
            _ => query.OrderByDescending(p => p.DateUpdated)
        };

        return query;
    }

    private Task<PaginatedList<Models.Media>> FetchMediaAsync(QueryMediaCommand request, ZauberDbContext dbContext, CancellationToken cancellationToken)
    {
        var query = BuildQuery(request, dbContext);
        return Task.FromResult(query.ToPaginatedList(request.PageIndex, request.AmountPerPage));
    }
}
