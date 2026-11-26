namespace ZauberCMS.Core.Shared.Models;

/// <summary>
/// The "Show more" row at the end of a tree node whose children are shown a page at a time (Roadmap D26). Choosing
/// it loads the next page in its place.
/// </summary>
public class TreeLoadMore
{
    /// <summary>How many children a tree shows at once.</summary>
    public const int PageSize = 100;

    /// <summary>The node whose children this row continues; null for the top level.</summary>
    public Guid? ParentId { get; set; }

    /// <summary>How many children are already shown.</summary>
    public int Skip { get; set; }

    /// <summary>How many children the node has in all.</summary>
    public int TotalItems { get; set; }

    /// <summary>The row's text (trees show <c>Name</c>).</summary>
    public string Name => $"Show {Math.Min(PageSize, TotalItems - Skip)} more ({TotalItems - Skip} not shown)";

    /// <summary>The row that follows <paramref name="level"/>, or null when the level is the last page.</summary>
    public static TreeLoadMore? After<T>(TreeLevel<T> level, Guid? parentId) => level.HasMore
        ? new TreeLoadMore { ParentId = parentId, Skip = level.Skip + level.Items.Count, TotalItems = level.TotalItems }
        : null;
}
