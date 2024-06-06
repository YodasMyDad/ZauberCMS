using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ZauberCMS.Core.Content.Commands;
using ZauberCMS.Core.Data;
using ZauberCMS.Core.Extensions;
using ZauberCMS.Core.Shared.Models;

namespace ZauberCMS.Core.Content.Handlers;

public class QueryContentHandler(IServiceProvider serviceProvider)
    : IRequestHandler<QueryContentCommand, PaginatedList<Models.Content>>
{
    public Task<PaginatedList<Models.Content>> Handle(QueryContentCommand request, CancellationToken cancellationToken)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ZauberDbContext>();
        var query = dbContext.Content.Include(x => x.ContentType).AsQueryable();

        if (request.IncludeChildren)
        {
            query = query.Include(x => x.Children);
            query = query.AsSplitQuery();
        }
        
        if (request.Query != null)
        {
            query = request.Query;
        }
        
        if (request.AsNoTracking)
        {
            query = query.AsNoTracking();
        }
        
        if (!request.SearchTerm.IsNullOrWhiteSpace())
        {
            query = query.Where(x => x.Name != null && x.Name.ToLower().Contains(request.SearchTerm.ToLower()));
        }

        if (!request.ContentTypeAlias.IsNullOrWhiteSpace())
        {
            var contentType = dbContext.ContentTypes.AsNoTracking().FirstOrDefault(x => x.Alias == request.ContentTypeAlias);
            if (contentType != null)
            {
                request.ContentTypeId = contentType.Id;
            }
        }
        
        if(request.ContentTypeId != null)
        {
            query = query.Where(x => x.ContentTypeId == request.ContentTypeId);
        }

        var idCount = request.Ids.Count;
        if (request.Ids.Count != 0)
        {
            query = query.Where(x => request.Ids.Contains(x.Id));
            request.AmountPerPage = idCount;
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
        
        return Task.FromResult(query.ToPaginatedList(request.PageIndex, request.AmountPerPage));
    }
}