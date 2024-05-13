using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ZauberCMS.Core.Content.Commands;
using ZauberCMS.Core.Data;
using ZauberCMS.Core.Extensions;
using ZauberCMS.Core.Shared.Models;

namespace ZauberCMS.Core.Content.Handlers;

public class GetContentsHandler(IServiceProvider serviceProvider)
    : IRequestHandler<GetContentsCommand, PaginatedList<Models.Content>>
{
    public Task<PaginatedList<Models.Content>> Handle(GetContentsCommand request, CancellationToken cancellationToken)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ZauberDbContext>();
        var query = dbContext.Content.AsQueryable();

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
            GetContentsOrderBy.DateUpdated => query.OrderBy(p => p.DateUpdated),
            GetContentsOrderBy.DateUpdatedDescending => query.OrderByDescending(p => p.DateUpdated),
            GetContentsOrderBy.DateCreated => query.OrderBy(p => p.DateCreated),
            GetContentsOrderBy.DateCreatedDescending => query.OrderByDescending(p => p.DateCreated),
            _ => query.OrderByDescending(p => p.DateUpdated)
        };
        
        return Task.FromResult(query.ToPaginatedList(request.PageIndex, request.AmountPerPage));
    }
}