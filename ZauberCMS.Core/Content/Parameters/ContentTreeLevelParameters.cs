namespace ZauberCMS.Core.Content.Parameters;

/// <summary>A page of an admin content tree node: <see cref="ParentId"/>'s children, or the root pages.</summary>
public class ContentTreeLevelParameters
{
    /// <summary>The node whose children to load; null for the root pages.</summary>
    public Guid? ParentId { get; set; }

    /// <summary>Instead of a node's children, exactly these items (for a tree given its top level by its caller).</summary>
    public List<Guid>? Ids { get; set; }

    /// <summary>How many children to skip (those already shown).</summary>
    public int Skip { get; set; }

    /// <summary>How many to load.</summary>
    public int Take { get; set; } = Shared.Models.TreeLoadMore.PageSize;

    /// <summary>Include unpublished pages (the admin trees do).</summary>
    public bool IncludeUnpublished { get; set; } = true;
}
