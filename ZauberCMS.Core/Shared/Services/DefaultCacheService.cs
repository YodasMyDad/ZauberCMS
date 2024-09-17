using System.Collections.Concurrent;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using ZauberCMS.Core.Settings;
using CacheExtensions = ZauberCMS.Core.Extensions.CacheExtensions;

namespace ZauberCMS.Core.Shared.Services;

public class DefaultCacheService : ICacheService
{
    private readonly IMemoryCache _memoryCache;
    private readonly IDistributedCache _distributedCache;
    private readonly bool _useRedis;
    private static readonly ConcurrentDictionary<string, byte> Keys = new();

    public DefaultCacheService(IMemoryCache memoryCache, IDistributedCache distributedCache, IOptions<ZauberSettings> settings)
    {
        _memoryCache = memoryCache;
        _distributedCache = distributedCache;

        // Check if Redis is configured
        var redisConnectionString = settings.Value.RedisConnectionString;
        _useRedis = !string.IsNullOrEmpty(redisConnectionString);
    }

    public async Task<T?> GetSetCachedItemAsync<T>(string cacheKey, Func<Task<T>> getCacheItemAsync, int cacheTimeInMinutes = CacheExtensions.MemoryCacheInMinutes)
    {
        if (_useRedis)
        {
            // Use Redis cache
            var cachedData = await _distributedCache.GetAsync(cacheKey);
            if (cachedData != null)
            {
                return Deserialize<T>(cachedData);
            }

            var value = await getCacheItemAsync();
            var data = Serialize(value);

            var options = new DistributedCacheEntryOptions()
                .SetSlidingExpiration(TimeSpan.FromMinutes(cacheTimeInMinutes));

            await _distributedCache.SetAsync(cacheKey, data, options);
            Keys.TryAdd(cacheKey, default);

            return value;
        }

        // Use Memory cache
        if (!_memoryCache.TryGetValue(cacheKey, out T? cacheEntry))
        {
            cacheEntry = await getCacheItemAsync();

            var cacheEntryOptions = new MemoryCacheEntryOptions()
                .SetSlidingExpiration(TimeSpan.FromMinutes(cacheTimeInMinutes));

            _memoryCache.Set(cacheKey, cacheEntry, cacheEntryOptions);
            Keys.TryAdd(cacheKey, default);
        }
        return cacheEntry;
    }

    public T? GetSetCachedItem<T>(string cacheKey, Func<T> getCacheItem, int cacheTimeInMinutes = CacheExtensions.MemoryCacheInMinutes)
    {
        if (_useRedis)
        {
            // Use Redis cache
            var cachedData = _distributedCache.Get(cacheKey);
            if (cachedData != null)
            {
                return Deserialize<T>(cachedData);
            }

            var value = getCacheItem();
            var data = Serialize(value);

            var options = new DistributedCacheEntryOptions()
                .SetSlidingExpiration(TimeSpan.FromMinutes(cacheTimeInMinutes));

            _distributedCache.Set(cacheKey, data, options);
            Keys.TryAdd(cacheKey, default);

            return value;
        }

        // Use Memory cache
        if (!_memoryCache.TryGetValue(cacheKey, out T? cacheEntry))
        {
            cacheEntry = getCacheItem();

            var cacheEntryOptions = new MemoryCacheEntryOptions()
                .SetSlidingExpiration(TimeSpan.FromMinutes(cacheTimeInMinutes));

            _memoryCache.Set(cacheKey, cacheEntry, cacheEntryOptions);
            Keys.TryAdd(cacheKey, default);
        }
        return cacheEntry;
    }

    public void ClearCachedItem(string cacheKey)
    {
        if (_useRedis)
        {
            _distributedCache.Remove(cacheKey);
        }
        else
        {
            _memoryCache.Remove(cacheKey);
        }
        Keys.TryRemove(cacheKey, out _);
    }

    public void ClearCachedItemsWithPrefix(string cacheKeyPrefix)
    {
        foreach (var key in Keys.Keys)
        {
            if (key.StartsWith(cacheKeyPrefix, StringComparison.OrdinalIgnoreCase))
            {
                if (_useRedis)
                {
                    _distributedCache.Remove(key);
                }
                else
                {
                    _memoryCache.Remove(key);
                }
                Keys.TryRemove(key, out _);
            }
        }
    }

    private static byte[] Serialize<T>(T value)
    {
        return System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(value);
    }

    private static T? Deserialize<T>(byte[] data)
    {
        return System.Text.Json.JsonSerializer.Deserialize<T>(data);
    }
}
