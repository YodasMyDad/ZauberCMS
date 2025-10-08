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
    
    // Change tracking - store actual Content objects to preserve in-memory modifications
    public List<Content> AddedItems { get; init; } = [];
    public List<Content> UpdatedItems { get; init; } = [];
    public List<Content> DeletedItems { get; init; } = [];
    
    public BlockListChanges GetChanges()
    {
        return new BlockListChanges
        {
            AddedItems = AddedItems,
            UpdatedItems = UpdatedItems,
            DeletedItems = DeletedItems
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
        
        var newAddedItems = new List<Content>(AddedItems);
        if (!newAddedItems.Any(x => x.Id == content.Id))
        {
            newAddedItems.Add(content);
        }
        
        return this with 
        { 
            Items = newItems,
            AddedItems = newAddedItems
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
            
            var newAddedItems = new List<Content>(AddedItems);
            var newUpdatedItems = new List<Content>(UpdatedItems);
            
            // If this was just added, update the AddedItems list with the new version
            var addedIndex = newAddedItems.FindIndex(x => x.Id == content.Id);
            if (addedIndex >= 0)
            {
                newAddedItems[addedIndex] = content;
            }
            else
            {
                // Not in added list, so track as update
                var updatedIndex = newUpdatedItems.FindIndex(x => x.Id == content.Id);
                if (updatedIndex >= 0)
                {
                    newUpdatedItems[updatedIndex] = content;
                }
                else
                {
                    newUpdatedItems.Add(content);
                }
            }
            
            return this with 
            { 
                Items = newItems,
                AddedItems = newAddedItems,
                UpdatedItems = newUpdatedItems
            };
        }
        
        return this;
    }
    
    /// <summary>
    /// Delete a content item
    /// </summary>
    public BlockListState DeleteItem(Guid contentId)
    {
        var itemToDelete = Items.FirstOrDefault(x => x.Id == contentId);
        if (itemToDelete == null) return this;
        
        var newItems = Items.Where(x => x.Id != contentId).ToList();
        var newAddedItems = new List<Content>(AddedItems);
        var newUpdatedItems = new List<Content>(UpdatedItems);
        var newDeletedItems = new List<Content>(DeletedItems);
        
        // If item was just added, remove it from added list (no need to delete from DB)
        var wasAdded = newAddedItems.Any(x => x.Id == contentId);
        if (wasAdded)
        {
            newAddedItems.RemoveAll(x => x.Id == contentId);
        }
        else
        {
            // Store the actual content object for deletion processing
            if (!newDeletedItems.Any(x => x.Id == contentId))
            {
                newDeletedItems.Add(itemToDelete);
            }
        }
        
        // Remove from updated list if present
        newUpdatedItems.RemoveAll(x => x.Id == contentId);
        
        return this with 
        { 
            Items = newItems,
            AddedItems = newAddedItems,
            UpdatedItems = newUpdatedItems,
            DeletedItems = newDeletedItems
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
        
        var newAddedItems = new List<Content>(AddedItems);
        var newUpdatedItems = new List<Content>(UpdatedItems);
        
        // Mark all items as updated when reordering (order matters for rendering)
        // But don't mark newly added items - they're already tracked
        foreach (var content in newItems)
        {
            var isAdded = newAddedItems.Any(x => x.Id == content.Id);
            if (!isAdded)
            {
                var updatedIndex = newUpdatedItems.FindIndex(x => x.Id == content.Id);
                if (updatedIndex >= 0)
                {
                    newUpdatedItems[updatedIndex] = content;
                }
                else
                {
                    newUpdatedItems.Add(content);
                }
            }
        }
        
        return this with 
        { 
            Items = newItems,
            AddedItems = newAddedItems,
            UpdatedItems = newUpdatedItems
        };
    }
    
    
    /// <summary>
    /// Merge nested changes from child block list editors
    /// </summary>
    public BlockListState MergeNestedChanges(BlockListChanges nestedChanges)
    {
        var newAddedItems = new List<Content>(AddedItems);
        var newUpdatedItems = new List<Content>(UpdatedItems);
        var newDeletedItems = new List<Content>(DeletedItems);

        // Merge added items
        foreach (var item in nestedChanges.AddedItems)
        {
            if (!newAddedItems.Any(x => x.Id == item.Id))
            {
                newAddedItems.Add(item);
            }
        }

        // Merge updated items
        foreach (var item in nestedChanges.UpdatedItems)
        {
            // Only add to updated if not in added list
            var isAdded = newAddedItems.Any(x => x.Id == item.Id);
            if (!isAdded)
            {
                var updatedIndex = newUpdatedItems.FindIndex(x => x.Id == item.Id);
                if (updatedIndex >= 0)
                {
                    newUpdatedItems[updatedIndex] = item;
                }
                else
                {
                    newUpdatedItems.Add(item);
                }
            }
        }

        // Merge deleted items
        foreach (var item in nestedChanges.DeletedItems)
        {
            // Add to deleted items if not already present
            if (!newDeletedItems.Any(x => x.Id == item.Id))
            {
                newDeletedItems.Add(item);
            }
            // Remove from added/updated lists
            newAddedItems.RemoveAll(x => x.Id == item.Id);
            newUpdatedItems.RemoveAll(x => x.Id == item.Id);
        }

        return this with
        {
            AddedItems = newAddedItems,
            UpdatedItems = newUpdatedItems,
            DeletedItems = newDeletedItems
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

