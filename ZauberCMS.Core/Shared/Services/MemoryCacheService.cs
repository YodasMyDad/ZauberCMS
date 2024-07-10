using System.Collections.Concurrent;
using Microsoft.Extensions.Caching.Memory;
using CacheExtensions = ZauberCMS.Core.Extensions.CacheExtensions;

namespace ZauberCMS.Core.Shared.Services;

public class MemoryCacheService(IMemoryCache cache) : ICacheService
{
    private static readonly ConcurrentDictionary<string, byte> Keys = new();

    public async Task<T?> GetSetCachedItemAsync<T>(string cacheKey, Func<Task<T>> getCacheItemAsync, int cacheTimeInMinutes = CacheExtensions.MemoryCacheInMinutes)
    {
        // Look for cache key.
        if (!cache.TryGetValue(cacheKey, out T? cacheEntry))
        {
            // Key not in cache, so get data.
            cacheEntry = await getCacheItemAsync();

            // Set cache options.
            var cacheEntryOptions = new MemoryCacheEntryOptions()
                // Keep in cache for this time, reset time if accessed.
                .SetSlidingExpiration(TimeSpan.FromMinutes(cacheTimeInMinutes));

            // Save data in cache.
            cache.Set(cacheKey, cacheEntry, cacheEntryOptions);
            Keys.TryAdd(cacheKey, default);
        }

        return cacheEntry;
    }
    
    public T? GetSetCachedItem<T>(string cacheKey, Func<T> getCacheItem, int cacheTimeInMinutes = CacheExtensions.MemoryCacheInMinutes)
    {
        // Look for cache key.
        if (!cache.TryGetValue(cacheKey, out T? cacheEntry))
        {
            // Key not in cache, so get data.
            cacheEntry = getCacheItem();

            // Set cache options.
            var cacheEntryOptions = new MemoryCacheEntryOptions()
                // Keep in cache for this time, reset time if accessed.
                .SetSlidingExpiration(TimeSpan.FromMinutes(cacheTimeInMinutes));

            // Save data in cache.
            cache.Set(cacheKey, cacheEntry, cacheEntryOptions);
            Keys.TryAdd(cacheKey, default);
        }

        return cacheEntry;
    }
    
    public void ClearCachedItem(string cacheKey)
    {
        cache.Remove(cacheKey);
        Keys.TryRemove(cacheKey, out _);
    }
    
    public void ClearCachedItemsWithPrefix(string cacheKeyPrefix)
    {
        foreach(var key in Keys.Keys)
        {
            if(key.StartsWith(cacheKeyPrefix, StringComparison.OrdinalIgnoreCase))
            {
                cache.Remove(key);
                Keys.TryRemove(key, out _);
            }
        }
    }
}