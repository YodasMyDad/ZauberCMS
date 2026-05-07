using System.Collections.Generic;
using System.Linq;

namespace ZauberCMS.Core.Content.Models;

public class BlockListChanges
{
    public List<Content> AddedItems { get; set; } = [];
    public List<Content> UpdatedItems { get; set; } = [];
    public List<Content> DeletedItems { get; set; } = [];
    public bool HasChanges => AddedItems.Any() || UpdatedItems.Any() || DeletedItems.Any();

    /// <summary>
    /// Returns a new <see cref="BlockListChanges"/> with each list deduplicated by ContentId
    /// (last entry wins) and any item present in <see cref="DeletedItems"/> stripped from the
    /// added/updated lists. Same logic that ContentEditor.ProcessBlockListChanges has applied
    /// client-side; exposed here so server callers (custom services / jobs) get the same safety net.
    /// </summary>
    public BlockListChanges Deduplicated()
    {
        var deletedDistinct = DeletedItems
            .GroupBy(x => x.Id)
            .Select(g => g.Last())
            .ToList();
        var deletedIds = deletedDistinct.Select(x => x.Id).ToHashSet();

        var addedDistinct = AddedItems
            .GroupBy(x => x.Id)
            .Select(g => g.Last())
            .Where(x => !deletedIds.Contains(x.Id))
            .ToList();

        var updatedDistinct = UpdatedItems
            .GroupBy(x => x.Id)
            .Select(g => g.Last())
            .Where(x => !deletedIds.Contains(x.Id))
            .ToList();

        return new BlockListChanges
        {
            AddedItems = addedDistinct,
            UpdatedItems = updatedDistinct,
            DeletedItems = deletedDistinct
        };
    }
}
