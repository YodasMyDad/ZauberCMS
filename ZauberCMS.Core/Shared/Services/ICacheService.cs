using ZauberCMS.Core.Extensions;

namespace ZauberCMS.Core.Shared.Services;

public interface ICacheService
{
    Task<T?> GetSetCachedItemAsync<T>(string cacheKey, Func<Task<T>> getCacheItemAsync, int cacheTimeInMinutes = CacheExtensions.MemoryCacheInMinutes);
    T? GetSetCachedItem<T>(string cacheKey, Func<T> getCacheItem, int cacheTimeInMinutes = CacheExtensions.MemoryCacheInMinutes);
    void ClearCachedItem(string cacheKey);
    void ClearCachedItemsWithPrefix(string cacheKeyPrefix);
}