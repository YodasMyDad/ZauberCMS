using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ZauberCMS.Core.Data;
using ZauberCMS.Core.Extensions;
using ZauberCMS.Core.Membership.Commands;
using ZauberCMS.Core.Membership.Models;
using ZauberCMS.Core.Shared.Models;

namespace ZauberCMS.Core.Membership.Handlers;

public class QueryRolesHandler(IServiceProvider serviceProvider)
    : IRequestHandler<QueryRolesCommand, PaginatedList<Role>>
{
    public Task<PaginatedList<Role>> Handle(QueryRolesCommand request, CancellationToken cancellationToken)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ZauberDbContext>();
        var query = dbContext.Roles.AsQueryable();

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

            var idCount = request.Ids.Count;
            if (request.Ids.Count != 0)
            {
                query = query.Where(x => request.Ids.Contains(x.Id));
                request.AmountPerPage = idCount;
            }
        }
        
        if (request.WhereClause != null)
        {
            query = query.Where(request.WhereClause);
        }   

        query = request.OrderBy switch
        {
            GetRolesOrderBy.DateCreated => query.OrderBy(p => p.DateCreated),
            GetRolesOrderBy.DateCreatedDescending => query.OrderByDescending(p => p.DateCreated),
            GetRolesOrderBy.DateUpdated => query.OrderBy(p => p.DateUpdated),
            GetRolesOrderBy.DateUpdatedDescending => query.OrderByDescending(p => p.DateUpdated),
            GetRolesOrderBy.Name => query.OrderBy(p => p.Name),
            _ => query.OrderByDescending(p => p.DateCreated)
        };
        
        return Task.FromResult(query.ToPaginatedList(request.PageIndex, request.AmountPerPage));
    }
}