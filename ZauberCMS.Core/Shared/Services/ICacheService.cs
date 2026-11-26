using ZauberCMS.Core.Extensions;

namespace ZauberCMS.Core.Shared.Services;

public interface ICacheService
{
    Task<T?> GetSetCachedItemAsync<T>(string cacheKey, Func<Task<T>> getCacheItemAsync, int cacheTimeInMinutes = CacheExtensions.MemoryCacheInMinutes);
    Task<T?> GetSetCachedItemAsync<T>(string cacheKey, Func<Task<T>> getCacheItemAsync, int cacheTimeInMinutes, int cacheTimeInSeconds);
    T? GetSetCachedItem<T>(string cacheKey, Func<T> getCacheItem, int cacheTimeInMinutes = CacheExtensions.MemoryCacheInMinutes);
    T? GetSetCachedItem<T>(string cacheKey, Func<T> getCacheItem, int cacheTimeInMinutes, int cacheTimeInSeconds);
    /// <summary>
    /// Like <see cref="GetSetCachedItemAsync{T}(string, Func{Task{T}}, int)"/>, but keeps the loaded value only
    /// when <paramref name="shouldCache"/> says so (for example: not a miss whose key comes from the visitor). The
    /// default implementation, for caches written before it existed, always keeps it.
    /// </summary>
    Task<T?> GetSetCachedItemAsync<T>(string cacheKey, Func<Task<T>> getCacheItemAsync, int cacheTimeInMinutes,
        Func<T, bool> shouldCache) => GetSetCachedItemAsync(cacheKey, getCacheItemAsync, cacheTimeInMinutes);

    void ClearCachedItem(string cacheKey);
    void ClearCachedItemsWithPrefix(string cacheKeyPrefix);

}