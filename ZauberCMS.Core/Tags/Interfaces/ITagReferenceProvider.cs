namespace ZauberCMS.Core.Tags.Interfaces;

/// <summary>
/// Tells ZauberCMS which tags a property value holds, for one property editor. The content save writes an item's
/// tag links (<c>TagItem</c> rows, which the tag filters of content queries read) from the values it publishes, in
/// the same transaction, the way Umbraco persists tags with the content (Roadmap Phase 10b, ZA-099). A "Save only"
/// draft does not change them; they follow the live values. A property editor that stores tags in its own format
/// implements this. Discovered like other extensions: a public, non-abstract class in a scanned assembly; a site's
/// own provider for an editor wins over the built-in one.
/// </summary>
public interface ITagReferenceProvider
{
    /// <summary>
    /// The alias of the property editor whose values this reads (<c>IContentProperty.Alias</c>, stored on the
    /// property as <c>PropertyType.ComponentAlias</c>).
    /// </summary>
    string EditorAlias { get; }

    /// <summary>The ids of the tags the value holds. Never null.</summary>
    /// <param name="value">The stored property value; may be null, empty or not in the editor's format.</param>
    IReadOnlyCollection<Guid> GetTagIds(string? value);
}
