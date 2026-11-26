using System.Collections.Generic;
using ZauberCMS.Core.Membership.Models;
using ZauberCMS.Core.Shared.Models;

namespace ZauberCMS.Core.Content.Parameters;

public class SaveContentParameters
{
    public Content.Models.Content? Content { get; set; }
    public List<Role> Roles { get; set; } = [];
    public bool UpdateContentRoles { get; set; }
    /// <summary>
    /// Save the item's own fields (name, URL, place in the tree, language, …) but not its property values. Use it
    /// for changes that are not an edit of the content: sort, move, restrict access, culture, restore. Such a save
    /// keeps a pending draft (<see cref="SaveUnpublishedOnly"/>); a save with property data replaces it.
    /// </summary>
    public bool ExcludePropertyData { get; set; }

    /// <summary>
    /// Store the values as a pending draft of a published item and leave the live item as it is, blocks included:
    /// edited blocks get drafts too. The draft, with its blocks' drafts, is published by the next save of the item
    /// that includes property data.
    /// </summary>
    public bool SaveUnpublishedOnly { get; set; }

    /// <summary>
    /// The blocks (nested content) that were added or edited anywhere below <see cref="Content"/>, saved with it as
    /// one change. Order does not matter, and a block the item's values no longer reach (removed, or its edit
    /// cancelled) is ignored. Blocks not listed are kept as stored. Which blocks the item has, and their order, comes
    /// from its block list values; blocks it no longer reaches are deleted. Ignored with
    /// <see cref="ExcludePropertyData"/>.
    /// </summary>
    public List<Content.Models.Content> Blocks { get; set; } = [];
}