namespace ZauberCMS.Core.Content.Interfaces;

/// <summary>
/// Tells ZauberCMS which nested content items (blocks) a property value owns, for one property editor. Core uses it
/// wherever it walks a tree of blocks: saving a page with its blocks, deleting, version snapshots, copying and
/// validation. A property editor that stores blocks in its own format (a grid or layout editor) implements this so
/// its blocks are saved, deleted, versioned and copied with the page. Discovered like other extensions: a public,
/// non-abstract class in a scanned assembly.
/// </summary>
public interface INestedContentReferenceProvider
{
    /// <summary>
    /// The alias of the property editor whose values this reads (<c>IContentProperty.Alias</c>, stored on the
    /// property as <c>PropertyType.ComponentAlias</c>).
    /// </summary>
    string EditorAlias { get; }

    /// <summary>The ids of the nested content items the value points at, in their order. Never null.</summary>
    /// <param name="value">The stored property value; may be null, empty or not in the editor's format.</param>
    IReadOnlyList<Guid> GetReferencedIds(string? value);

    /// <summary>
    /// The value with each id that is a key in <paramref name="map"/> replaced by the mapped id (a copy of a page
    /// gets copies of its blocks). Ids that are not in the map stay as they are.
    /// </summary>
    string? RemapIds(string? value, IReadOnlyDictionary<Guid, Guid> map);
}
