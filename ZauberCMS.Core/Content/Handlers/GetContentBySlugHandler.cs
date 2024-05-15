using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ZauberCMS.Core.Content.Commands;
using ZauberCMS.Core.Data;

namespace ZauberCMS.Core.Content.Handlers;

public class GetContentBySlugHandler(IServiceProvider serviceProvider)
    : IRequestHandler<GetContentBySlugCommand, Models.Content?>
{
    public async Task<Models.Content?> Handle(GetContentBySlugCommand request, CancellationToken cancellationToken)
    {
        //TODO - Need to sanitise and check slug
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ZauberDbContext>();
        
        // If is RootContent we just get the first one we can find
        var query = dbContext.Content
            .Include(x => x.Parent)
            .Include(x => x.ContentType)
            .AsNoTracking();
        
        // If this is root content, get the first one
        var content = request.IsRootContent
            ? await query
                .FirstOrDefaultAsync(c => c.IsRootContent, cancellationToken: cancellationToken)
            : await query
                .FirstOrDefaultAsync(c => c.Url == request.Slug, cancellationToken: cancellationToken);

        // If this content has an internal redirect id, return that content instead
        if (content?.InternalRedirectId != null)
        {
            content = await query
                .FirstOrDefaultAsync(c => c.Id == content.InternalRedirectId.Value, cancellationToken: cancellationToken);
        }
        
        return content;
    }
}