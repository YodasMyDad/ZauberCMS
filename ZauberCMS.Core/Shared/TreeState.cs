using System.Collections.Concurrent;

namespace ZauberCMS.Core.Shared;

public class TreeState
{
    // ConcurrentDictionary to store the IDs of expanded nodes
    private readonly ConcurrentDictionary<Guid, byte> _expandedNodeIds = new();

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
}