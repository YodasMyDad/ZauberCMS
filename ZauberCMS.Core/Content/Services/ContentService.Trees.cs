using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ZauberCMS.Core.Content.Parameters;
using ZauberCMS.Core.Data;
using ZauberCMS.Core.Shared.Models;

namespace ZauberCMS.Core.Content.Services;

// The admin content trees: a page of a node's children at a time (Roadmap D26).
public partial class ContentService
{
    /// <inheritdoc />
    public async Task<TreeLevel<Models.Content>> GetContentTreeLevelAsync(ContentTreeLevelParameters parameters,
        CancellationToken cancellationToken = default)
    {
        using var scope = serviceScopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IZauberDbContext>();

        // Pages only: blocks and the recycle bin are never in a content tree
        var query = dbContext.Contents.AsNoTracking().Where(c => !c.Deleted && !c.IsNestedContent);
        if (parameters.Ids != null)
        {
            var ids = parameters.Ids;
            query = query.Where(c => ids.Contains(c.Id));
        }
        else
        {
            query = parameters.ParentId == null
                ? query.Where(c => c.IsRootContent)
                : query.Where(c => c.ParentId == parameters.ParentId);
        }

        if (!parameters.IncludeUnpublished)
        {
            query = query.Where(c => c.Published);
        }

        // What a node shows (name, icon, state, the padlock) and what the context menus read, without the property
        // values and children every node used to load (ZA-068)
        var items = await query
            .OrderBy(c => c.SortOrder).ThenBy(c => c.DateCreated).ThenBy(c => c.Id)
            .Skip(parameters.Skip).Take(parameters.Take)
            .Include(c => c.ContentType)
            .Include(c => c.ContentRoles)
            .AsSplitQuery()
            .ToListAsync(cancellationToken);
        var total = parameters.Skip == 0 && items.Count < parameters.Take
            ? items.Count
            : await query.CountAsync(cancellationToken);

        var itemIds = items.Select(c => c.Id).ToList();
        var withChildren = await dbContext.Contents.AsNoTracking()
            .Where(c => c.ParentId != null && itemIds.Contains(c.ParentId.Value) && !c.Deleted && !c.IsNestedContent)
            .Select(c => c.ParentId!.Value)
            .Distinct()
            .ToListAsync(cancellationToken);

        return new TreeLevel<Models.Content>
        {
            Items = items,
            IdsWithChildren = withChildren.ToHashSet(),
            TotalItems = total,
            Skip = parameters.Skip
        };
    }
}
