using System.Collections.Generic;

namespace ZauberCMS.Core.Content.Models;

public class BlockListChanges
{
    public List<Content> AddedItems { get; set; } = [];
    public List<Content> UpdatedItems { get; set; } = [];
    public List<Content> DeletedItems { get; set; } = [];
    public bool HasChanges => AddedItems.Any() || UpdatedItems.Any() || DeletedItems.Any();
}
