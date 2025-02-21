using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ZauberCMS.Core.Data;
using ZauberCMS.Core.Extensions;
using ZauberCMS.Core.Seo.Commands;
using ZauberCMS.Core.Seo.Models;
using ZauberCMS.Core.Shared.Services;

namespace ZauberCMS.Core.Seo.Handlers;

public class QueryRedirectsHandler(IServiceProvider serviceProvider, ICacheService cacheService)
    : IRequestHandler<QueryRedirectsCommand, List<SeoRedirect>>
{
    public async Task<List<SeoRedirect>> Handle(QueryRedirectsCommand request, CancellationToken cancellationToken)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ZauberDbContext>();
        var query = BuildQuery(request, dbContext);
        var cacheKey = query.GenerateCacheKey(typeof(SeoRedirect));
        
        if (request.Cached)
        {
            return (await cacheService.GetSetCachedItemAsync(cacheKey, async () => await FetchContentAsync(request, dbContext, cancellationToken)))!;
        }

        return await FetchContentAsync(request, dbContext, cancellationToken);
    }

    private static IQueryable<SeoRedirect> BuildQuery(QueryRedirectsCommand request, ZauberDbContext dbContext)
    {
        var query = dbContext.Redirects.AsQueryable();

        if (request.Query != null)
        {
            query = request.Query;
            return query;
        }

        if (request.AsNoTracking)
        {
            query = query.AsNoTracking();
        }
        
        if (request.Ids.Count > 0)
        {
            query = query.Where(p => request.Ids.Contains(p.Id));
        }
        
        query = request.OrderBy switch
        {
            GetSeoRedirectOrderBy.DateCreated => query.OrderBy(p => p.DateCreated),
            GetSeoRedirectOrderBy.DateCreatedDescending => query.OrderByDescending(p => p.DateCreated),
            GetSeoRedirectOrderBy.DateUpdated => query.OrderBy(p => p.DateUpdated),
            GetSeoRedirectOrderBy.DateUpdatedDescending => query.OrderBy(p => p.DateUpdated),
            _ => query.OrderByDescending(p => p.FromUrl)
        };

        return query.Take(request.Amount);
    }
    
    private static async Task<List<SeoRedirect>> FetchContentAsync(QueryRedirectsCommand request, ZauberDbContext dbContext, CancellationToken cancellationToken)
    {
        var query = BuildQuery(request, dbContext);
        return await query.ToListAsync(cancellationToken: cancellationToken);
    }
}