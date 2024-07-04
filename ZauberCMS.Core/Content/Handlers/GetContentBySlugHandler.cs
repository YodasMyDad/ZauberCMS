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
        // TODO - Need to sanitize and check slug
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ZauberDbContext>();

        // If this is root content, get the first one with minimal data
        var content = request.IsRootContent
            ? await dbContext.Contents
                .AsNoTracking()
                .Where(c => c.IsRootContent)
                .Select(c => new { c.Id, c.InternalRedirectId })
                .FirstOrDefaultAsync(cancellationToken: cancellationToken)
            : await dbContext.Contents
                .AsNoTracking()
                .Where(c => c.Url == request.Slug)
                .Select(c => new { c.Id, c.InternalRedirectId })
                .FirstOrDefaultAsync(cancellationToken: cancellationToken);

        // If this content has an internal redirect id, get that content's ID instead
        if (content?.InternalRedirectId != null && content.InternalRedirectId != Guid.Empty && !request.IgnoreInternalRedirect)
        {
            var internalRedirectIdValue = content.InternalRedirectId.Value;
            content = await dbContext.Contents
                .AsNoTracking()
                .Where(c => c.Id == internalRedirectIdValue)
                .Select(c => new { c.Id, c.InternalRedirectId })
                .FirstOrDefaultAsync(cancellationToken: cancellationToken);
        }

        if (content == null)
        {
            return null;
        }

        // Now we perform the more expensive query to fetch the content with includes
        var query = dbContext.Contents
            .AsNoTracking()
            .Include(x => x.PropertyData)
            .Include(x => x.Parent)
            .Include(x => x.ContentType)
            .AsSplitQuery()
            .AsQueryable();

        if (request.IncludeChildren)
        {
            query = query.Include(x => x.Children);
        }

        var fullContent = await query
            .FirstOrDefaultAsync(c => c.Id == content.Id, cancellationToken: cancellationToken);

        return fullContent;
    }
    
    /*public async Task<Models.Content?> Handle(GetContentBySlugCommand request, CancellationToken cancellationToken)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ZauberDbContext>();
        
        // If is RootContent we just get the first one we can find
        var query = dbContext.Contents
            .AsNoTracking()
            .Include(x => x.PropertyData)
            .Include(x => x.Parent)
            .Include(x => x.ContentType)
            .AsSplitQuery()
            .AsQueryable();
        
        if (request.IncludeChildren)
        {
            query = query.Include(x => x.Children);
        }
        
        // If this is root content, get the first one
        var content = request.IsRootContent
            ? await query
                .FirstOrDefaultAsync(c => c.IsRootContent, cancellationToken: cancellationToken)
            : await query
                .FirstOrDefaultAsync(c => c.Url == request.Slug, cancellationToken: cancellationToken);

        // If this content has an internal redirect id, return that content instead
        if (content?.InternalRedirectId != null && content.InternalRedirectId != Guid.Empty && !request.IgnoreInternalRedirect)
        {
            content = await query
                .FirstOrDefaultAsync(c => c.Id == content.InternalRedirectId.Value, cancellationToken: cancellationToken);
        }
        
        return content;
    }*/
}