using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ZauberCMS.Core.Content.Commands;
using ZauberCMS.Core.Content.Models;
using ZauberCMS.Core.Data;
using ZauberCMS.Core.Extensions;
using ZauberCMS.Core.Shared.Models;

namespace ZauberCMS.Core.Content.Handlers;

public class QueryContentTypesHandler(IServiceProvider serviceProvider)
    : IRequestHandler<QueryContentTypesCommand, PaginatedList<ContentType>>
{
    public Task<PaginatedList<ContentType>> Handle(QueryContentTypesCommand request, CancellationToken cancellationToken)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ZauberDbContext>();
        var query = dbContext.ContentTypes.AsQueryable();

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
        
            if (!request.SearchTerm.IsNullOrWhiteSpace())
            {
#pragma warning disable CA1862
                query = query.Where(x => x.Name != null && x.Name.ToLower().Contains(request.SearchTerm.ToLower()));
#pragma warning restore CA1862
            }

            if (request.ElementTypesOnly != null)
            {
                query = query.Where(x => x.IsElementType == request.ElementTypesOnly.Value);
            }
        
            if (request.RootOnly)
            {
                query = query.Where(x => x.AllowAtRoot);
            }
        }
        
        if (request.WhereClause != null)
        {
            query = query.Where(request.WhereClause);
        }
        
        query = request.OrderBy switch
        {
            GetContentTypesOrderBy.DateUpdated => query.OrderBy(p => p.DateUpdated),
            GetContentTypesOrderBy.DateUpdatedDescending => query.OrderByDescending(p => p.DateUpdated),
            GetContentTypesOrderBy.DateCreated => query.OrderBy(p => p.DateCreated),
            GetContentTypesOrderBy.DateCreatedDescending => query.OrderByDescending(p => p.DateCreated),
            GetContentTypesOrderBy.Name => query.OrderBy(p => p.Name),
            _ => query.OrderByDescending(p => p.DateUpdated)
        };
        
        return Task.FromResult(query.ToPaginatedList(request.PageIndex, request.AmountPerPage));
    }
}