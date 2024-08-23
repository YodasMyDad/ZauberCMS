using System.Linq.Dynamic.Core;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ZauberCMS.Core.Content.Commands;
using ZauberCMS.Core.Data;
using ZauberCMS.Core.Extensions;
using ZauberCMS.Core.Shared.Models;

namespace ZauberCMS.Core.Content.Handlers;

public class DataGridContentHandler(IServiceProvider serviceProvider) : IRequestHandler<DataGridContentCommand, DataGridResult<Models.Content>>
{
    public async Task<DataGridResult<Models.Content>> Handle(DataGridContentCommand request, CancellationToken cancellationToken)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ZauberDbContext>();
        
        var result = new DataGridResult<Models.Content>();

        // Now you have the DbSet<T> and can query it
        var query = dbContext.Contents
            .Include(x => x.ContentType)
            .Include(x => x.LastUpdatedBy)
            .AsQueryable();
        
        if (request.IncludeChildren)
        {
            query = query.Include(x => x.Children);
            query = query.AsSplitQuery();
        }
        
        if (request.AsNoTracking)
        {
            query = query.AsNoTracking();
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
            
        if(request.ParentId != null)
        {
            query = query.Where(x => x.ParentId == request.ParentId);
        }
        
        if (!string.IsNullOrEmpty(request.Filter))
        {
            // Filter via the Where method
            query = query.Where(request.Filter);
        }

        if (!string.IsNullOrEmpty(request.Order))
        {
            // Sort via the OrderBy method
            query = query.OrderBy(request.Order);
        }
        else
        {
            query = request.OrderBy switch
            {
                GetContentsOrderBy.DateUpdated => query.OrderBy(p => p.DateUpdated),
                GetContentsOrderBy.DateUpdatedDescending => query.OrderByDescending(p => p.DateUpdated),
                GetContentsOrderBy.DateCreated => query.OrderBy(p => p.DateCreated),
                GetContentsOrderBy.DateCreatedDescending => query.OrderByDescending(p => p.DateCreated),
                GetContentsOrderBy.SortOrder => query.OrderBy(p => p.SortOrder),
                _ => query.OrderByDescending(p => p.DateUpdated)
            };
        }

        // Important!!! Make sure the Count property of RadzenDataGrid is set.
        result.Count = query.Count();

        // Perform paging via Skip and Take.
        result.Items = await query.Skip(request.Skip).Take(request.Take).ToListAsync(cancellationToken: cancellationToken);

        return result;
    }
}