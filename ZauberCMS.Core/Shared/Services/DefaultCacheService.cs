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

        return await LoadOnceAsync(cacheKey, async () =>
        {
            var generation = serverCache.Generation;
            var value = await getCacheItemAsync();
            serverCache.Set(cacheKey, value, Expiration(cacheTimeInMinutes, cacheTimeInSeconds), generation);
            return value;
        });
    }

    public async Task<T?> GetSetCachedItemAsync<T>(string cacheKey, Func<Task<T>> getCacheItemAsync,
        int cacheTimeInMinutes, Func<T, bool> shouldCache)
    {
        if (serverCache.TryGet(cacheKey, out T? cached))
        {
            return cached;
        }

        return await LoadOnceAsync(cacheKey, async () =>
        {
            var generation = serverCache.Generation;
            var value = await getCacheItemAsync();
            if (shouldCache(value))
            {
                serverCache.Set(cacheKey, value, Expiration(cacheTimeInMinutes, 0), generation);
            }

            return value;
        });
    }

    // Loads under way (ServerCache.Loading, shared by every request on the server). Requests that miss the same key
    // at the same moment wait for one load instead of each sending the same query to the database (ZA-115). A load
    // only serves requests that arrive before the next clear: one that started before a save is not handed to a
    // request made after it.
    private sealed record Loading(Task Task, long Generation);

    private async Task<T> LoadOnceAsync<T>(string cacheKey, Func<Task<T>> load)
    {
        var mine = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        var entry = new Loading(mine.Task, serverCache.Generation);
        var running = (Loading)serverCache.Loading.GetOrAdd(cacheKey, entry);
        if (running != entry)
        {
            if (running.Task is Task<T> shared && running.Generation == serverCache.Generation)
            {
                try
                {
                    return await shared;
                }
                catch (OperationCanceledException)
                {
                    // The request that started the load went away; this one still wants the value
                }
            }

            return await load();
        }

        try
        {
            var value = await load();
            mine.SetResult(value);
            return value;
        }
        catch (OperationCanceledException)
        {
            mine.SetCanceled();
            throw;
        }
        catch (Exception ex)
        {
            mine.SetException(ex);
            // Seen here, so a load nobody else waited for is not reported as an unobserved exception
            _ = mine.Task.Exception;
            throw;
        }
        finally
        {
            serverCache.Loading.TryRemove(new KeyValuePair<string, object>(cacheKey, entry));
        }
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
