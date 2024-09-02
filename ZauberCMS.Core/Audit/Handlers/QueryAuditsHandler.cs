using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ZauberCMS.Core.Audit.Commands;
using ZauberCMS.Core.Data;
using ZauberCMS.Core.Extensions;
using ZauberCMS.Core.Shared.Models;

namespace ZauberCMS.Core.Audit.Handlers;

public class QueryAuditsHandler(IServiceProvider serviceProvider)
    : IRequestHandler<QueryAuditsCommand, PaginatedList<Models.Audit>>
{
    public Task<PaginatedList<Models.Audit>> Handle(QueryAuditsCommand request, CancellationToken cancellationToken)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ZauberDbContext>();
        var query = dbContext.Audits.AsQueryable();

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
        }

        /*if (request.Username != null)
        {
            query = query.Where(x => x.Username == request.Username);
        }*/
        
        if (request.WhereClause != null)
        {
            query = query.Where(request.WhereClause);
        }   
        
        
        query = request.OrderBy switch
        {
            GetAuditsOrderBy.DateCreated => query.OrderBy(p => p.DateCreated),
            GetAuditsOrderBy.DateCreatedDescending => query.OrderByDescending(p => p.DateCreated),
            _ => query.OrderByDescending(p => p.DateCreated)
        };
        
        return Task.FromResult(query.ToPaginatedList(request.PageIndex, request.AmountPerPage));
    }
}