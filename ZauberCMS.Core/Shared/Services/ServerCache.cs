using System.Collections.Concurrent;
using Microsoft.Extensions.Caching.Memory;

namespace ZauberCMS.Core.Shared.Services;

/// <summary>
/// The cache entries on this server: <see cref="IMemoryCache"/> plus an index of the keys ZauberCMS stored, so a
/// prefix can be cleared. Registered as a singleton; <see cref="DefaultCacheService"/> is the way to read and write
/// it. Entities are cached as the objects they are (never serialised), so their navigations and role lists stay as
/// loaded. Other servers are told about clears through <see cref="ICacheInvalidationBus"/>; a custom bus applies the
/// clears it receives with <see cref="Remove"/>, <see cref="RemoveWithPrefix"/> and <see cref="RemoveAll"/>, which
/// do not publish anything.
/// </summary>
public sealed class ServerCache(IMemoryCache memoryCache)
{
    private const int LockStripes = 64;

    // Key -> the entry's own token. An eviction only removes the index entry when the token is still its own, so a
    // replaced entry can't take its successor's key out of the index (ZA-055).
    private readonly ConcurrentDictionary<string, object> _keys = new(StringComparer.Ordinal);

    // Writing the index and the value, and removing them, happen under the key's lock, so the two always agree.
    private readonly object[] _locks = Enumerable.Range(0, LockStripes).Select(_ => new object()).ToArray();

    // Bumped by every clear. A value loaded while a clear ran may be stale, so it is returned but not kept.
    private long _generation;

    internal long Generation => Interlocked.Read(ref _generation);

    internal int Count => _keys.Count;

    /// <summary>
    /// Loads under way, by key, for <see cref="DefaultCacheService"/>: kept here, on the singleton, so requests (each
    /// with its own scoped cache service) that miss the same key together share one load (ZA-115).
    /// </summary>
    internal ConcurrentDictionary<string, object> Loading { get; } = new(StringComparer.Ordinal);

    internal bool TryGet<T>(string key, out T? value) => memoryCache.TryGetValue(key, out value);

    internal void Set<T>(string key, T value, TimeSpan slidingExpiration, long generationAtLoad)
    {
        lock (LockFor(key))
        {
            if (Generation != generationAtLoad)
            {
                return;
            }

            var token = new object();
            _keys[key] = token;
            memoryCache.Set(key, value, new MemoryCacheEntryOptions()
                .SetSlidingExpiration(slidingExpiration)
                .RegisterPostEvictionCallback(static (evictedKey, _, _, state) =>
                {
                    var (cache, entryToken) = ((ServerCache, object))state!;
                    cache.Untrack((string)evictedKey, entryToken);
                }, (this, token)));

            // A clear that started after the check above may have taken its list of keys before this one was
            // added; this entry may be stale, so drop it
            if (Generation != generationAtLoad)
            {
                _keys.TryRemove(new KeyValuePair<string, object>(key, token));
                memoryCache.Remove(key);
            }
        }
    }

    /// <summary>Removes one key from this server's cache (does not tell other servers).</summary>
    public void Remove(string key)
    {
        Interlocked.Increment(ref _generation);
        RemoveKey(key);
    }

    /// <summary>
    /// Removes every key starting with <paramref name="prefix"/> (case-insensitive) from this server's cache (does
    /// not tell other servers).
    /// </summary>
    public void RemoveWithPrefix(string prefix)
    {
        Interlocked.Increment(ref _generation);
        foreach (var key in _keys.Keys)
        {
            if (key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                RemoveKey(key);
            }
        }
    }

    /// <summary>Drops everything ZauberCMS cached on this server (when invalidations may have been missed).</summary>
    public void RemoveAll() => RemoveWithPrefix(string.Empty);

    private void RemoveKey(string key)
    {
        lock (LockFor(key))
        {
            _keys.TryRemove(key, out _);
            memoryCache.Remove(key);
        }
    }

    private void Untrack(string key, object token)
    {
        lock (LockFor(key))
        {
            _keys.TryRemove(new KeyValuePair<string, object>(key, token));
        }
    }

    private object LockFor(string key) => _locks[(key.GetHashCode() & int.MaxValue) % LockStripes];
}
