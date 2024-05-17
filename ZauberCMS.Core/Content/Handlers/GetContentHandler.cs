using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ZauberCMS.Core.Content.Commands;
using ZauberCMS.Core.Data;
using ZauberCMS.Core.Extensions;

namespace ZauberCMS.Core.Content.Handlers;

public class GetContentHandler (IServiceProvider serviceProvider) 
    : IRequestHandler<GetContentCommand, Models.Content?>
{
    public async Task<Models.Content?> Handle(GetContentCommand request, CancellationToken cancellationToken)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ZauberDbContext>();
        var query = dbContext.Content.Include(x => x.ContentType).AsNoTracking().AsQueryable();
        if (request.IncludeParent)
        {
            query = query.Include(x => x.Parent);
        }
        
        if (request.IncludeChildren)
        {
            query = query.Include(x => x.Children);
            
            if (request.IncludeParent)
            {
                // Make it a split query if parent is also included
                query = query.AsSplitQuery();
            }
        }

        if (request.Id != null)
        {
            return await query.FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken: cancellationToken);
        }
        
        if (!request.ContentTypeAlias.IsNullOrWhiteSpace())
        {
            return await query.FirstOrDefaultAsync(x => x.ContentType.Alias == request.ContentTypeAlias, cancellationToken: cancellationToken);
        }

        // Should never get here
        return await query.FirstOrDefaultAsync(cancellationToken: cancellationToken);
    }
}