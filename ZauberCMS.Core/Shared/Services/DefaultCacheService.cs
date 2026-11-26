using CacheExtensions = ZauberCMS.Core.Extensions.CacheExtensions;

namespace ZauberCMS.Core.Shared.Services;

/// <summary>
/// The default <see cref="ICacheService"/>: values live in this server's memory (<see cref="ServerCache"/>), and
/// every clear is passed to the other servers through <see cref="ICacheInvalidationBus"/> (Redis when
/// <c>Zauber:RedisConnectionString</c> is set). Nothing cached is ever written to Redis, so entity graphs keep their
/// navigations and no user data leaves the server (Roadmap D13).
/// </summary>
public class DefaultCacheService(ServerCache serverCache, ICacheInvalidationBus invalidationBus) : ICacheService
{
    public Task<T?> GetSetCachedItemAsync<T>(string cacheKey, Func<Task<T>> getCacheItemAsync,
        int cacheTimeInMinutes = CacheExtensions.MemoryCacheInMinutes) =>
        GetSetCachedItemAsync(cacheKey, getCacheItemAsync, cacheTimeInMinutes, 0);

    public async Task<T?> GetSetCachedItemAsync<T>(string cacheKey, Func<Task<T>> getCacheItemAsync,
        int cacheTimeInMinutes, int cacheTimeInSeconds)
    {
        if (serverCache.TryGet(cacheKey, out T? cached))
        {
            return cached;
        }

        var generation = serverCache.Generation;
        var value = await getCacheItemAsync();
        serverCache.Set(cacheKey, value, Expiration(cacheTimeInMinutes, cacheTimeInSeconds), generation);
        return value;
    }

    public async Task<T?> GetSetCachedItemAsync<T>(string cacheKey, Func<Task<T>> getCacheItemAsync,
        int cacheTimeInMinutes, Func<T, bool> shouldCache)
    {
        if (serverCache.TryGet(cacheKey, out T? cached))
        {
            return cached;
        }

        var generation = serverCache.Generation;
        var value = await getCacheItemAsync();
        if (shouldCache(value))
        {
            serverCache.Set(cacheKey, value, Expiration(cacheTimeInMinutes, 0), generation);
        }

        return value;
    }

    public T? GetSetCachedItem<T>(string cacheKey, Func<T> getCacheItem,
        int cacheTimeInMinutes = CacheExtensions.MemoryCacheInMinutes) =>
        GetSetCachedItem(cacheKey, getCacheItem, cacheTimeInMinutes, 0);

    public T? GetSetCachedItem<T>(string cacheKey, Func<T> getCacheItem, int cacheTimeInMinutes,
        int cacheTimeInSeconds)
    {
        if (serverCache.TryGet(cacheKey, out T? cached))
        {
            return cached;
        }

        var generation = serverCache.Generation;
        var value = getCacheItem();
        serverCache.Set(cacheKey, value, Expiration(cacheTimeInMinutes, cacheTimeInSeconds), generation);
        return value;
    }

    public void ClearCachedItem(string cacheKey)
    {
        serverCache.Remove(cacheKey);
        invalidationBus.PublishKeyCleared(cacheKey);
    }

    public void ClearCachedItemsWithPrefix(string cacheKeyPrefix)
    {
        serverCache.RemoveWithPrefix(cacheKeyPrefix);
        invalidationBus.PublishPrefixCleared(cacheKeyPrefix);
    }

    private static TimeSpan Expiration(int cacheTimeInMinutes, int cacheTimeInSeconds) =>
        TimeSpan.FromMinutes(cacheTimeInMinutes).Add(TimeSpan.FromSeconds(cacheTimeInSeconds));
}
