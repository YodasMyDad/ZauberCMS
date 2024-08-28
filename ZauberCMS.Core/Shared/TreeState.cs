using System.Collections.Concurrent;

namespace ZauberCMS.Core.Shared;

public class TreeState
{
    // ConcurrentDictionary to store the IDs of expanded nodes
    private readonly ConcurrentDictionary<Guid, byte> _expandedNodeIds = new();

    // Initialize the cache
    public readonly ConcurrentDictionary<Guid, bool> HasChildrenCache = new();

    public event Action<object>? OnTreeValueChanged;

    private object? _treeValue;

    public object? TreeValue
    {
        get => _treeValue;
        set
        {
            if (_treeValue != value)
            {
                _treeValue = value;
                if (_treeValue != null) OnTreeValueChanged?.Invoke(_treeValue);
            }
        }
    }

    // Method to expand a node
    public void NodeExpanded(Guid nodeId)
    {
        _expandedNodeIds[nodeId] = 0;
    }

    // Method to collapse a node
    public void NodeCollapsed(Guid nodeId)
    {
        _expandedNodeIds.TryRemove(nodeId, out _);
    }

    // Method to check if a node is expanded
    public bool IsNodeExpanded(Guid nodeId)
    {
        return _expandedNodeIds.ContainsKey(nodeId);
    }

    // Method to clear all nodes
    public void ClearNodes()
    {
        _expandedNodeIds.Clear();
    }

    public bool HasChildren(Guid nodeId)
    {
        return HasChildrenCache.TryGetValue(nodeId, out var hasChildren) && hasChildren;
    }
    
    public void SetChildren(Guid nodeId, bool hasChildren)
    {
        HasChildrenCache[nodeId] = hasChildren;
    }

    public void ClearChildCache(Guid? contentId)
    {
        if (contentId != null)
        {
            HasChildrenCache.TryRemove(contentId.Value, out _);
        }
        else
        {
            HasChildrenCache.Clear();
        }
    }
}