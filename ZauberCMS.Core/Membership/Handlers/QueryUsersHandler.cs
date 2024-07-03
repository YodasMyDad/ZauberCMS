using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ZauberCMS.Core.Data;
using ZauberCMS.Core.Extensions;
using ZauberCMS.Core.Membership.Commands;
using ZauberCMS.Core.Membership.Models;
using ZauberCMS.Core.Shared.Models;

namespace ZauberCMS.Core.Membership.Handlers;

public class QueryUsersHandler(IServiceProvider serviceProvider)
    : IRequestHandler<QueryUsersCommand, PaginatedList<User>>
{
    public Task<PaginatedList<User>> Handle(QueryUsersCommand request, CancellationToken cancellationToken)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ZauberDbContext>();
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
        
            if (!request.SearchTerm.IsNullOrWhiteSpace())
            {
                query = query.Where(x => x.UserName != null && x.UserName.ToLower().Contains(request.SearchTerm.ToLower()));
            }

            if (request.Roles.Count != 0)
            {
                query = query.Where(x => x.UserRoles.Any(ur => ur.Role.Name != null && request.Roles.Contains(ur.Role.Name)));
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
            GetUsersOrderBy.DateUpdated => query.OrderBy(p => p.DateCreated),
            GetUsersOrderBy.DateCreatedDescending => query.OrderByDescending(p => p.DateCreated),
            _ => query.OrderByDescending(p => p.DateCreated)
        };
        
        return Task.FromResult(query.ToPaginatedList(request.PageIndex, request.AmountPerPage));
    }
}