using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ZauberCMS.Core.Data;
using ZauberCMS.Core.Media.Commands;

namespace ZauberCMS.Core.Media.Handlers;

public class GetMediaHandler(IServiceProvider serviceProvider)
    : IRequestHandler<GetMediaCommand, Models.Media?>
{
    public async Task<Models.Media?> Handle(GetMediaCommand request, CancellationToken cancellationToken)
    {
        using (var scope = serviceProvider.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ZauberDbContext>();
            var query = dbContext.Medias.AsQueryable();

            if (request.AsNoTracking)
            {
                query = query.AsNoTracking();
            }

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

            if (request.MediaType != null)
            {
                query = query.Where(x => x.MediaType == request.MediaType);
            }

            if (request.Id != null)
            {
                return await query.FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken: cancellationToken);
            }

            // Should never get here
            return await query.FirstOrDefaultAsync(cancellationToken: cancellationToken);
        }
    }
}