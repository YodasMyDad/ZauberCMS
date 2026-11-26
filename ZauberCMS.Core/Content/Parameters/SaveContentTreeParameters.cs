namespace ZauberCMS.Core.Content.Parameters;

/// <summary>
/// Saves a content item together with its blocks (nested content), as one change: see
/// <c>IContentService.SaveContentTreeAsync</c>.
/// </summary>
public class SaveContentTreeParameters
{
    /// <summary>The content item (usually a page) with its new values, block lists included.</summary>
    public Models.Content? Content { get; set; }

    /// <summary>
    /// The blocks that were added or edited, anywhere below <see cref="Content"/>. Order does not matter, and a block
    /// the new values no longer reach (it was removed, or its edit was cancelled) is ignored. Blocks not listed are
    /// kept as they are stored. Which blocks belong to the item, and in what order, comes from the block list values.
    /// </summary>
    public List<Models.Content> Blocks { get; set; } = [];

    /// <summary>
    /// Store the values as a pending draft of a published item, blocks included, and leave the live site as it is
    /// (see <see cref="SaveContentParameters.SaveUnpublishedOnly"/>). The next save without it publishes the item's
    /// draft and its blocks' drafts.
    /// </summary>
    public bool SaveUnpublishedOnly { get; set; }
}
