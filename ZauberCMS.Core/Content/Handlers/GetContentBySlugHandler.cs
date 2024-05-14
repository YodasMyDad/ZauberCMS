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
        var content = request.IsRootContent
            ? await dbContext.Content
                .AsNoTracking()
                .Include(c => c.ContentType)
                .FirstOrDefaultAsync(c => c.IsRootContent, cancellationToken: cancellationToken)
            : await dbContext.Content
                .AsNoTracking()
                .Include(c => c.ContentType)
                .FirstOrDefaultAsync(c => c.Url == request.Slug, cancellationToken: cancellationToken);
        return content;
    }
}