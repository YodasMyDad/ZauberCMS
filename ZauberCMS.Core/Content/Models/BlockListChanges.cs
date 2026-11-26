using System.Collections.Generic;
using System.Linq;

namespace ZauberCMS.Core.Content.Models;

/// <summary>
/// No longer used by ZauberCMS: the admin editor saves a page and its blocks together with
/// <c>IContentService.SaveContentAsync</c> with <c>SaveContentParameters.Blocks</c>, which works out what was added, changed and removed.
/// </summary>
[Obsolete("Save the page with IContentService.SaveContentAsync and SaveContentParameters.Blocks; it works out added, changed and removed blocks.")]
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
