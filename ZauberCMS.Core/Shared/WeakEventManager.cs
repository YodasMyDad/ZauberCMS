using System.Reflection;
using Microsoft.Extensions.Logging;

namespace ZauberCMS.Core.Shared;

/// <summary>
/// Manages weak event subscriptions to prevent memory leaks when event sources outlive subscribers.
/// Use this for Singleton services that raise events consumed by Scoped components.
/// Holds a WeakReference to the handler's target plus its MethodInfo so the subscription stays alive
/// as long as the subscribing object is alive — and is reclaimed automatically when it is collected.
/// </summary>
/// <typeparam name="TEventArgs">The event arguments type</typeparam>
public class WeakEventManager<TEventArgs>
{
    private sealed record Subscription(
        WeakReference<object>? Target,
        Func<TEventArgs, string, Task>? StaticDelegate,
        MethodInfo Method);

    private readonly List<Subscription> _handlers = [];
    private readonly Lock _lock = new();
    private readonly ILogger? _logger;

    public WeakEventManager()
    {
    }

    public WeakEventManager(ILogger? logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Adds an event handler. The handler's target is held weakly; the subscription is removed
    /// automatically once the target is garbage collected. Static handlers are held strongly
    /// (they have no target to weakly reference).
    /// </summary>
    public void AddHandler(Func<TEventArgs, string, Task> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);

        var sub = handler.Target is null
            ? new Subscription(null, handler, handler.Method)
            : new Subscription(new WeakReference<object>(handler.Target), null, handler.Method);

        lock (_lock)
        {
            CleanupDeadReferences();
            _handlers.Add(sub);
        }
    }

    /// <summary>
    /// Removes an event handler. Matches by both target identity and MethodInfo, so multiple
    /// instance handlers pointing to the same method (on different objects) are distinguished.
    /// </summary>
    public void RemoveHandler(Func<TEventArgs, string, Task> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);

        lock (_lock)
        {
            _handlers.RemoveAll(sub =>
            {
                if (!sub.Method.Equals(handler.Method))
                {
                    return false;
                }

                if (sub.StaticDelegate is not null)
                {
                    return handler.Target is null;
                }

                if (sub.Target is null)
                {
                    return false;
                }

                if (!sub.Target.TryGetTarget(out var target))
                {
                    // Dead anyway — prune.
                    return true;
                }

                return ReferenceEquals(target, handler.Target);
            });
        }
    }

    /// <summary>
    /// Raises the event to all alive subscribers. Handler exceptions are logged and swallowed
    /// so a single faulty subscriber cannot break the rest.
    /// </summary>
    public async Task RaiseEventAsync(TEventArgs args, string username)
    {
        List<Func<TEventArgs, string, Task>> handlers;

        lock (_lock)
        {
            CleanupDeadReferences();
            handlers = new List<Func<TEventArgs, string, Task>>(_handlers.Count);
            foreach (var sub in _handlers)
            {
                if (sub.StaticDelegate is not null)
                {
                    handlers.Add(sub.StaticDelegate);
                    continue;
                }

                if (sub.Target is not null && sub.Target.TryGetTarget(out var target))
                {
                    var del = (Func<TEventArgs, string, Task>)sub.Method.CreateDelegate(
                        typeof(Func<TEventArgs, string, Task>), target);
                    handlers.Add(del);
                }
            }
        }

        // Invoke handlers outside lock to prevent deadlocks
        foreach (var handler in handlers)
        {
            try
            {
                await handler(args, username);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex,
                    "Error invoking weak event handler {Method} on {DeclaringType}",
                    handler.Method.Name,
                    handler.Method.DeclaringType?.FullName);
            }
        }
    }

    /// <summary>
    /// Removes handlers whose targets have been garbage collected.
    /// </summary>
    private void CleanupDeadReferences()
    {
        _handlers.RemoveAll(sub => sub.Target is not null && !sub.Target.TryGetTarget(out _));
    }
}
