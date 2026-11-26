namespace ZauberCMS.Core.Shared.Models;

/// <summary>
/// One page of a tree node's children, with which of them have children of their own, so a tree can show its
/// expand arrows without a query per node.
/// </summary>
public class TreeLevel<T>
{
    /// <summary>The items of this page, in tree order.</summary>
    public List<T> Items { get; set; } = [];

    /// <summary>The ids of the items that have children.</summary>
    public HashSet<Guid> IdsWithChildren { get; set; } = [];

    /// <summary>How many items the node has in all.</summary>
    public int TotalItems { get; set; }

    /// <summary>How many items come before this page.</summary>
    public int Skip { get; set; }

    /// <summary>True when the node has items after this page.</summary>
    public bool HasMore => Skip + Items.Count < TotalItems;
}
