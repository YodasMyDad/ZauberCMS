using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ZauberCMS.Core.Data;
using ZauberCMS.Core.Extensions;
using ZauberCMS.Core.Membership.Commands;
using ZauberCMS.Core.Membership.Models;
using ZauberCMS.Core.Shared.Models;
using ZauberCMS.Core.Shared.Services;

namespace ZauberCMS.Core.Membership.Handlers;

public class QueryUsersHandler(IServiceProvider serviceProvider, ICacheService cacheService)
    : IRequestHandler<QueryUsersCommand, PaginatedList<User>>
{
    public async Task<PaginatedList<User>> Handle(QueryUsersCommand request, CancellationToken cancellationToken)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ZauberDbContext>();
        var query = BuildQuery(request, dbContext);
        var cacheKey = query.GenerateCacheKey(typeof(User));

        if (request.Cached)
        {
            return (await cacheService.GetSetCachedItemAsync(cacheKey, async () => await FetchUsersAsync(request, dbContext, cancellationToken)))!;
        }

        return await FetchUsersAsync(request, dbContext, cancellationToken);
    }

    private static IQueryable<User> BuildQuery(QueryUsersCommand request, ZauberDbContext dbContext)
    {
        var query = dbContext.Users.Include(x => x.UserRoles).AsQueryable();

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

            if (!string.IsNullOrWhiteSpace(request.SearchTerm))
            {
                var searchTermLower = request.SearchTerm.ToLower();
                query = query.Where(x => x.UserName != null && x.UserName.ToLower().Contains(searchTermLower));
            }

            if (request.Roles.Count != 0)
            {
                query = query.Where(x => x.UserRoles.Any(ur => ur.Role.Name != null && request.Roles.Contains(ur.Role.Name)));
            }

            if (request.Ids.Count != 0)
            {
                query = query.Where(x => request.Ids.Contains(x.Id));
                request.AmountPerPage = request.Ids.Count;
            }
        }

        if (request.WhereClause != null)
        {
            query = query.Where(request.WhereClause);
        }

        query = request.OrderBy switch
        {
            GetUsersOrderBy.DateUpdated => query.OrderBy(p => p.DateCreated),
            GetUsersOrderBy.DateCreatedDescending => query.OrderByDescending(p => p.DateCreated),
            _ => query.OrderByDescending(p => p.DateCreated)
        };

        return query;
    }

    private static Task<PaginatedList<User>> FetchUsersAsync(QueryUsersCommand request, ZauberDbContext dbContext, CancellationToken cancellationToken)
    {
        var query = BuildQuery(request, dbContext);
        return Task.FromResult(query.ToPaginatedList(request.PageIndex, request.AmountPerPage));
    }
}
