using ZauberCMS.Core.Content.Models;

namespace ZauberCMS.Components.Editors.Models;

/// <summary>
/// Immutable state manager for BlockList editor
/// Handles all content operations and change tracking cleanly
/// </summary>
public record BlockListState
{
    public List<Content> Items { get; init; } = [];
    public Dictionary<Guid, ContentType> ContentTypes { get; init; } = [];
    
    // Change tracking - using HashSets for automatic deduplication
    public HashSet<Guid> AddedIds { get; init; } = [];
    public HashSet<Guid> UpdatedIds { get; init; } = [];
    public HashSet<Guid> DeletedIds { get; init; } = [];
    
    public BlockListChanges GetChanges()
    {
        return new BlockListChanges
        {
            AddedItems = Items.Where(x => AddedIds.Contains(x.Id)).ToList(),
            UpdatedItems = Items.Where(x => UpdatedIds.Contains(x.Id)).ToList(),
            DeletedItems = Items.Where(x => DeletedIds.Contains(x.Id)).ToList()
        };
    }
    
    /// <summary>
    /// Add a new content item
    /// </summary>
    public BlockListState AddItem(Content content, int? insertAtIndex = null)
    {
        var newItems = new List<Content>(Items);
        
        if (insertAtIndex.HasValue && insertAtIndex.Value >= 0 && insertAtIndex.Value <= newItems.Count)
        {
            newItems.Insert(insertAtIndex.Value, content);
        }
        else
        {
            newItems.Add(content);
        }
        
        var newAddedIds = new HashSet<Guid>(AddedIds) { content.Id };
        
        return this with 
        { 
            Items = newItems,
            AddedIds = newAddedIds
        };
    }
    
    /// <summary>
    /// Update an existing content item
    /// </summary>
    public BlockListState UpdateItem(Content content)
    {
        var newItems = new List<Content>(Items);
        var index = newItems.FindIndex(x => x.Id == content.Id);
        
        if (index >= 0)
        {
            newItems[index] = content;
            
            var newUpdatedIds = new HashSet<Guid>(UpdatedIds);
            // Only mark as updated if it wasn't just added
            if (!AddedIds.Contains(content.Id))
            {
                newUpdatedIds.Add(content.Id);
            }
            
            return this with 
            { 
                Items = newItems,
                UpdatedIds = newUpdatedIds
            };
        }
        
        return this;
    }
    
    /// <summary>
    /// Delete a content item
    /// </summary>
    public BlockListState DeleteItem(Guid contentId)
    {
        var newItems = Items.Where(x => x.Id != contentId).ToList();
        var newDeletedIds = new HashSet<Guid>(DeletedIds);
        var newAddedIds = new HashSet<Guid>(AddedIds);
        var newUpdatedIds = new HashSet<Guid>(UpdatedIds);
        
        // If item was just added, remove it from added list (no need to delete from DB)
        if (AddedIds.Contains(contentId))
        {
            newAddedIds.Remove(contentId);
        }
        else
        {
            newDeletedIds.Add(contentId);
        }
        
        // Remove from updated list if present
        newUpdatedIds.Remove(contentId);
        
        return this with 
        { 
            Items = newItems,
            AddedIds = newAddedIds,
            UpdatedIds = newUpdatedIds,
            DeletedIds = newDeletedIds
        };
    }
    
    /// <summary>
    /// Reorder items by moving from oldIndex to newIndex
    /// </summary>
    public BlockListState ReorderItems(int oldIndex, int newIndex)
    {
        if (oldIndex < 0 || oldIndex >= Items.Count || newIndex < 0 || newIndex >= Items.Count)
        {
            return this;
        }
        
        var newItems = new List<Content>(Items);
        var item = newItems[oldIndex];
        newItems.RemoveAt(oldIndex);
        newItems.Insert(newIndex, item);
        
        // Mark all items as updated when reordering (order matters for rendering)
        var newUpdatedIds = new HashSet<Guid>(UpdatedIds);
        foreach (var content in newItems.Where(x => !AddedIds.Contains(x.Id)))
        {
            newUpdatedIds.Add(content.Id);
        }
        
        return this with 
        { 
            Items = newItems,
            UpdatedIds = newUpdatedIds
        };
    }
    
    /// <summary>
    /// Merge nested changes from child block list editors
    /// </summary>
    public BlockListState MergeNestedChanges(BlockListChanges nestedChanges)
    {
        var newAddedIds = new HashSet<Guid>(AddedIds);
        var newUpdatedIds = new HashSet<Guid>(UpdatedIds);
        var newDeletedIds = new HashSet<Guid>(DeletedIds);
        
        foreach (var item in nestedChanges.AddedItems)
        {
            newAddedIds.Add(item.Id);
        }
        
        foreach (var item in nestedChanges.UpdatedItems)
        {
            if (!newAddedIds.Contains(item.Id))
            {
                newUpdatedIds.Add(item.Id);
            }
        }
        
        foreach (var item in nestedChanges.DeletedItems)
        {
            newDeletedIds.Add(item.Id);
            newAddedIds.Remove(item.Id);
            newUpdatedIds.Remove(item.Id);
        }
        
        return this with
        {
            AddedIds = newAddedIds,
            UpdatedIds = newUpdatedIds,
            DeletedIds = newDeletedIds
        };
    }
    
    /// <summary>
    /// Add or update ContentType for preview rendering
    /// </summary>
    public BlockListState SetContentType(ContentType contentType)
    {
        var newContentTypes = new Dictionary<Guid, ContentType>(ContentTypes)
        {
            [contentType.Id] = contentType
        };
        
        return this with { ContentTypes = newContentTypes };
    }
    
    /// <summary>
    /// Get serialized IDs for storage
    /// </summary>
    public List<Guid> GetSerializedIds() => Items.Select(x => x.Id).ToList();
}

