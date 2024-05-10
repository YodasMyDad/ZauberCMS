using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ZauberCMS.Core.Content.Commands;
using ZauberCMS.Core.Content.Models;
using ZauberCMS.Core.Data;
using ZauberCMS.Core.Extensions;
using ZauberCMS.Core.Shared.Models;

namespace ZauberCMS.Core.Content.Handlers;

public class GetContentTypesHandler(IServiceProvider serviceProvider)
    : IRequestHandler<GetContentTypesCommand, PaginatedList<ContentType>>
{
    public Task<PaginatedList<ContentType>> Handle(GetContentTypesCommand request, CancellationToken cancellationToken)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ZauberDbContext>();
        var query = dbContext.ContentTypes.AsQueryable();

        if (request.AsNoTracking)
        {
            query = query.AsNoTracking();
        }
        
        if (!request.SearchTerm.IsNullOrWhiteSpace())
        {
            query = query.Where(x => x.Name != null && x.Name.ToLower().Contains(request.SearchTerm.ToLower()));
        }

        query = request.OrderBy switch
        {
            GetContentTypesOrderBy.DateUpdated => query.OrderBy(p => p.DateUpdated),
            GetContentTypesOrderBy.DateUpdatedDescending => query.OrderByDescending(p => p.DateUpdated),
            GetContentTypesOrderBy.DateCreated => query.OrderBy(p => p.DateCreated),
            GetContentTypesOrderBy.DateCreatedDescending => query.OrderByDescending(p => p.DateCreated),
            _ => query.OrderByDescending(p => p.DateUpdated)
        };
        
        return Task.FromResult(query.ToPaginatedList(request.PageIndex, request.AmountPerPage));
    }
}