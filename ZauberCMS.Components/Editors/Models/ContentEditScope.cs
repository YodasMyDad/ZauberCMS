using System.Text.RegularExpressions;
using ZauberCMS.Core.Content.Mapping;
using ZauberCMS.Core.Content.Models;

namespace ZauberCMS.Components.Editors.Models;

/// <summary>
/// The unsaved block edits of one content editor (Docs/BlockListEditor.md). The page's editor holds the root scope.
/// Opening a block opens a child scope of the editor it was opened from: what is edited there, blocks inside it
/// included, stays in that scope until its editor is saved (<see cref="Commit"/>, into the parent scope) or cancelled
/// (<see cref="Rollback"/>, gone). Nothing reaches the database until the page is saved, and then every block edit
/// goes in that one save (<c>IContentService.SaveContentAsync</c> with the root scope's <see cref="Drafts"/> as <c>Blocks</c>).
/// <para>
/// Which blocks a list holds, and their order, lives in the list's property value on the item that owns it, as it
/// always did; a scope only holds the edited blocks themselves. A block that was removed, or whose edit was cancelled
/// further up, is simply no longer reached and is ignored by the save.
/// </para>
/// A property editor that hosts nested content gets the scope of the editor it sits in as a cascading parameter.
/// Belongs to one circuit; not thread-safe.
/// </summary>
public sealed partial class ContentEditScope
{
    private const int MaxPreviewDepth = 20;

    private readonly Dictionary<Guid, Content> _drafts = new();

    private ContentEditScope(ContentEditScope? parent)
    {
        Parent = parent;
    }

    /// <summary>The scope of a page's (or other root item's) editor.</summary>
    public static ContentEditScope CreateRoot() => new(null);

    /// <summary>The scope this one commits into; null for the root.</summary>
    public ContentEditScope? Parent { get; }

    /// <summary>Committed or rolled back; it takes no more edits.</summary>
    public bool IsClosed { get; private set; }

    /// <summary>The blocks edited in this scope (and committed into it from its children).</summary>
    public IReadOnlyCollection<Content> Drafts => _drafts.Values;

    /// <summary>A scope for an editor opened from this one.</summary>
    public ContentEditScope BeginChild()
    {
        ThrowIfClosed();
        return new ContentEditScope(this);
    }

    /// <summary>The newest unsaved values of a block, from this scope or the scopes it sits in; null when the block
    /// has not been edited (read it from the database).</summary>
    public Content? GetDraft(Guid id)
    {
        for (var scope = this; scope != null; scope = scope.Parent)
        {
            if (scope._drafts.TryGetValue(id, out var draft)) return draft;
        }

        return null;
    }

    /// <summary>Records the block's new values in this scope. The object is kept as it is: don't change it after.</summary>
    public void SetDraft(Content draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        ThrowIfClosed();
        _drafts[draft.Id] = draft;
    }

    /// <summary>Keeps this scope's edits: they move to the parent scope. Does nothing if already closed.</summary>
    public void Commit()
    {
        if (IsClosed) return;
        if (Parent == null) throw new InvalidOperationException("The root scope is saved, not committed.");
        Parent.ThrowIfClosed();
        foreach (var (id, draft) in _drafts)
        {
            Parent._drafts[id] = draft;
        }

        Close();
    }

    /// <summary>Drops this scope's edits, those of blocks inside it included. Does nothing if already closed.</summary>
    public void Rollback()
    {
        if (IsClosed) return;
        if (Parent == null) throw new InvalidOperationException("The root scope is discarded, not rolled back.");
        Close();
    }

    /// <summary>
    /// A copy of <paramref name="item"/> for a block preview: <c>PendingBlockListChanges[alias]</c> holds the unsaved
    /// blocks whose ids appear in that property's value, in whatever format the editor stores them (copies of their
    /// own, all the way down), so a preview that calls
    /// <c>GetBlocks(alias, contentService, includePendingChanges: true)</c> shows the edits before the page is saved.
    /// </summary>
    public Content CreatePreviewCopy(Content item) => CreatePreviewCopy(item, 0, [item.Id]);

    private Content CreatePreviewCopy(Content item, int depth, HashSet<Guid> path)
    {
        var copy = item.MapToNew();
        copy.ContentType = item.ContentType;
        copy.PropertyData = item.PropertyData;
        if (depth >= MaxPreviewDepth) return copy;

        foreach (var value in item.PropertyData)
        {
            var ids = ParseIds(value.Value);
            if (ids == null) continue;

            Dictionary<Guid, Content>? pending = null;
            foreach (var id in ids)
            {
                var draft = GetDraft(id);
                if (draft == null || !path.Add(id)) continue;
                pending ??= new Dictionary<Guid, Content>();
                pending[id] = CreatePreviewCopy(draft, depth + 1, path);
                path.Remove(id);
            }

            if (pending != null)
            {
                copy.PendingBlockListChanges[value.Alias] = pending;
            }
        }

        return copy;
    }

    // Any GUID in the value: the Block List editor's JSON array, or a custom editor's own format
    [GeneratedRegex("[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}")]
    private static partial Regex GuidPattern();

    private static List<Guid>? ParseIds(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var ids = new List<Guid>();
        foreach (Match match in GuidPattern().Matches(value))
        {
            if (Guid.TryParse(match.Value, out var id) && id != Guid.Empty && !ids.Contains(id)) ids.Add(id);
        }

        return ids.Count == 0 ? null : ids;
    }

    private void Close()
    {
        IsClosed = true;
        _drafts.Clear();
    }

    private void ThrowIfClosed()
    {
        if (IsClosed) throw new InvalidOperationException("This edit scope was already committed or rolled back.");
    }
}
