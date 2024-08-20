using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ZauberCMS.Core.Content.Commands;
using ZauberCMS.Core.Content.Models;
using ZauberCMS.Core.Data;
using ZauberCMS.Core.Extensions;
using ZauberCMS.Core.Shared.Models;

namespace ZauberCMS.Core.Content.Handlers;

public class QueryDomainHandler(IServiceProvider serviceProvider)
    : IRequestHandler<QueryDomainCommand, PaginatedList<Domain>>
{
    public Task<PaginatedList<Domain>> Handle(QueryDomainCommand request, CancellationToken cancellationToken)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ZauberDbContext>();
        var query = dbContext.Domains.AsQueryable();

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

            if (request.ContentId != null)
            {
                query = query.Where(x => x.ContentId == request.ContentId);
            }

            if (request.LanguageId != null)
            {
                query = query.Where(x => x.LanguageId == request.LanguageId);
            }
        }

        if (request.WhereClause != null)
        {
            query = query.Where(request.WhereClause);
        }

        query = request.OrderBy switch
        {
            GetDomainOrderBy.DateCreated => query.OrderBy(p => p.DateCreated),
            GetDomainOrderBy.DateCreatedDescending => query.OrderByDescending(p => p.DateCreated),
            GetDomainOrderBy.Url => query.OrderBy(p => p.Url),
            _ => query.OrderByDescending(p => p.DateCreated)
        };

        return Task.FromResult(query.ToPaginatedList(request.PageIndex, request.AmountPerPage));
    }
}