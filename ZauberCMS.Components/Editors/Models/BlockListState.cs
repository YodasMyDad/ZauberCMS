using ZauberCMS.Core.Content.Models;

namespace ZauberCMS.Components.Editors.Models;

/// <summary>
/// What one Block List editor shows: its blocks in order and the element types they use. Immutable: every operation
/// returns a new state. Which blocks were changed is not tracked here; edits go into the editor's
/// <see cref="ContentEditScope"/>, and the list's order is the property value.
/// </summary>
public record BlockListState
{
    public List<Content> Items { get; init; } = [];
    public Dictionary<Guid, ContentType> ContentTypes { get; init; } = [];

    /// <summary>Adds a block at <paramref name="insertAtIndex"/>, or at the end.</summary>
    public BlockListState AddItem(Content content, int? insertAtIndex = null)
    {
        var newItems = new List<Content>(Items);
        if (insertAtIndex is >= 0 && insertAtIndex.Value <= newItems.Count)
        {
            newItems.Insert(insertAtIndex.Value, content);
        }
        else
        {
            newItems.Add(content);
        }

        return this with { Items = newItems };
    }

    /// <summary>Replaces the block with the same id; an unknown block changes nothing.</summary>
    public BlockListState UpdateItem(Content content)
    {
        var index = Items.FindIndex(x => x.Id == content.Id);
        if (index < 0) return this;

        var newItems = new List<Content>(Items) { [index] = content };
        return this with { Items = newItems };
    }

    /// <summary>Removes the block. The save deletes it, with the blocks inside it, once nothing lists it.</summary>
    public BlockListState DeleteItem(Guid contentId)
    {
        if (Items.All(x => x.Id != contentId)) return this;
        return this with { Items = Items.Where(x => x.Id != contentId).ToList() };
    }

    /// <summary>Moves the block at <paramref name="oldIndex"/> to <paramref name="newIndex"/>.</summary>
    public BlockListState ReorderItems(int oldIndex, int newIndex)
    {
        if (oldIndex < 0 || oldIndex >= Items.Count || newIndex < 0 || newIndex >= Items.Count || oldIndex == newIndex)
        {
            return this;
        }

        var newItems = new List<Content>(Items);
        var item = newItems[oldIndex];
        newItems.RemoveAt(oldIndex);
        newItems.Insert(newIndex, item);
        return this with { Items = newItems };
    }

    /// <summary>Adds or replaces an element type, for previews.</summary>
    public BlockListState SetContentType(ContentType contentType)
    {
        var newContentTypes = new Dictionary<Guid, ContentType>(ContentTypes)
        {
            [contentType.Id] = contentType
        };

        return this with { ContentTypes = newContentTypes };
    }

    /// <summary>The block ids in order: the property value.</summary>
    public List<Guid> GetSerializedIds() => Items.Select(x => x.Id).ToList();
}
