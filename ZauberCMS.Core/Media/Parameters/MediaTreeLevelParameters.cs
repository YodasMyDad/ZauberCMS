using ZauberCMS.Core.Media.Models;

namespace ZauberCMS.Core.Media.Parameters;

/// <summary>A page of an admin media tree node: <see cref="ParentId"/>'s children, or the top level.</summary>
public class MediaTreeLevelParameters
{
    /// <summary>The folder whose children to load; null for the top level.</summary>
    public Guid? ParentId { get; set; }

    /// <summary>Instead of a folder's children, exactly these items (for a tree given its top level by its caller).</summary>
    public List<Guid>? Ids { get; set; }

    /// <summary>How many children to skip (those already shown).</summary>
    public int Skip { get; set; }

    /// <summary>How many to load.</summary>
    public int Take { get; set; } = Shared.Models.TreeLoadMore.PageSize;

    /// <summary>Only these types (folders are always included); empty for all.</summary>
    public List<MediaType> MediaTypes { get; set; } = [];

    /// <summary>
    /// The recycle bin instead of the live media. With no <see cref="ParentId"/>, the items at the top of the bin
    /// (whose folder is not in it), most recently binned first; with one, what is in the bin inside that folder.
    /// </summary>
    public bool InRecycleBin { get; set; }
}
