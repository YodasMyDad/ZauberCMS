using System.Runtime.CompilerServices;

namespace ZauberCMS.Core.Shared;

/// <summary>
/// Manages weak event subscriptions to prevent memory leaks when event sources outlive subscribers.
/// Use this for Singleton services that raise events consumed by Scoped components.
/// </summary>
/// <typeparam name="TEventArgs">The event arguments type</typeparam>
public class WeakEventManager<TEventArgs>
{
    private readonly List<WeakReference<Func<TEventArgs, string, Task>>> _handlers = [];
    private readonly Lock _lock = new();

    /// <summary>
    /// Adds an event handler with weak reference to prevent memory leaks
    /// </summary>
    public void AddHandler(Func<TEventArgs, string, Task> handler)
    {
        lock (_lock)
        {
            // Remove dead references before adding new one
            CleanupDeadReferences();
            _handlers.Add(new WeakReference<Func<TEventArgs, string, Task>>(handler));
        }
    }

    /// <summary>
    /// Removes an event handler
    /// </summary>
    public void RemoveHandler(Func<TEventArgs, string, Task> handler)
    {
        lock (_lock)
        {
            _handlers.RemoveAll(wr =>
            {
                if (!wr.TryGetTarget(out var target))
                    return true; // Remove dead references
                return ReferenceEquals(target, handler);
            });
        }
    }

    /// <summary>
    /// Raises the event to all alive subscribers
    /// </summary>
    public async Task RaiseEventAsync(TEventArgs args, string username)
    {
        List<Func<TEventArgs, string, Task>> handlers;
        
        lock (_lock)
        {
            CleanupDeadReferences();
            handlers = _handlers
                .Select(wr => wr.TryGetTarget(out var target) ? target : null)
                .Where(h => h != null)
                .ToList()!;
        }

        // Invoke handlers outside lock to prevent deadlocks
        foreach (var handler in handlers)
        {
            try
            {
                await handler(args, username);
            }
            catch
            {
                // Swallow exceptions in handlers to not break other subscribers
            }
        }
    }

    /// <summary>
    /// Removes handlers that have been garbage collected
    /// </summary>
    private void CleanupDeadReferences()
    {
        _handlers.RemoveAll(wr => !wr.TryGetTarget(out _));
    }
}

