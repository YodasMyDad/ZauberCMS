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

        // Pages only, never blocks; the live tree or the recycle bin, never both
        var inBin = parameters.InRecycleBin;
        var query = dbContext.Contents.AsNoTracking().Where(c => c.Deleted == inBin && !c.IsNestedContent);
        if (parameters.Ids != null)
        {
            var ids = parameters.Ids;
            query = query.Where(c => ids.Contains(c.Id));
        }
        else if (parameters.ParentId != null)
        {
            query = query.Where(c => c.ParentId == parameters.ParentId);
        }
        else
        {
            // The top of the bin: what went in on its own or with a parent that is still live. The rest is listed,
            // restored and deleted below the item it went in with (ZA-156).
            query = inBin
                ? query.Where(c => c.ParentId == null || !dbContext.Contents.Any(p => p.Id == c.ParentId && p.Deleted))
                : query.Where(c => c.IsRootContent);
        }

        if (!parameters.IncludeUnpublished && !inBin)
        {
            query = query.Where(c => c.Published);
        }

        // The top of the bin shows the most recently binned first; a level below an item keeps the tree's order
        var ordered = inBin && parameters.ParentId == null && parameters.Ids == null
            ? query.OrderByDescending(c => c.DateUpdated).ThenBy(c => c.Id)
            : query.OrderBy(c => c.SortOrder).ThenBy(c => c.DateCreated).ThenBy(c => c.Id);

        // What a node shows (name, icon, state, the padlock) and what the context menus read, without the property
        // values and children every node used to load (ZA-068)
        var items = await ordered
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
            .Where(c => c.ParentId != null && itemIds.Contains(c.ParentId.Value) && c.Deleted == inBin && !c.IsNestedContent)
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
