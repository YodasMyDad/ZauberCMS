using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;

namespace ZauberCMS.Core.Extensions;

/// <summary>
/// Class to hold the cache keys used around the site
/// </summary>
public static class CacheExtensions
{
    public const int MemoryCacheInMinutes = 240;

    public static string ToCacheKey(this Type item, string identifier)
    {
        return $"{item.Name}-{identifier}";
    }
    
    public static string ToCacheKey(this Type item, List<string> identifier)
    {
        // need to order the items and CSV them to keep them consistent
        var cacheKey = string.Join("-", identifier.OrderBy(x => x));
        return $"{item.Name}-{cacheKey}";
    }
    
    public static string ToCacheKey(this Type item, List<Guid> identifier)
    {
        // need to order the items and CSV them to keep them consistent
        var cacheKey = string.Join("-", identifier.OrderBy(x => x));
        return $"{item.Name}-{cacheKey}";
    }

    /// <summary>
    /// Generates a cache key from an IQueryable by hashing the query string
    /// </summary>
    /// <typeparam name="T">The entity type for the cache key prefix</typeparam>
    /// <param name="query">The query to hash</param>
    /// <returns>Cache key in format: TypeName-Hash</returns>
    public static string GenerateCacheKey<T>(this IQueryable<T> query)
    {
        var queryString = query.ToQueryString();
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(queryString));
        return typeof(T).ToCacheKey(Convert.ToBase64String(hash));
    }

    /// <summary>
    /// Generates a cache key from parameters by creating a string representation and hashing it
    /// </summary>
    /// <typeparam name="T">The entity type for the cache key prefix</typeparam>
    /// <param name="parameters">Object containing parameters to hash</param>
    /// <param name="identifier">Optional additional identifier</param>
    /// <returns>Cache key in format: TypeName-Hash</returns>
    public static string GenerateCacheKey<T>(this object parameters, string? identifier = null)
    {
        var keyBuilder = new StringBuilder();
        
        // Add the identifier if provided
        if (!string.IsNullOrEmpty(identifier))
        {
            keyBuilder.Append($"{identifier}-");
        }
        
        // Add all public properties and their values
        var properties = parameters.GetType().GetProperties();
        foreach (var prop in properties.OrderBy(p => p.Name))
        {
            if (prop.CanRead)
            {
                var value = prop.GetValue(parameters);
                var stringValue = value?.ToString() ?? "null";
                keyBuilder.Append($"{prop.Name}:{stringValue}-");
            }
        }
        
        var key = keyBuilder.ToString().TrimEnd('-');
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(key));
        return typeof(T).ToCacheKey(Convert.ToBase64String(hash));
    }

    /// <summary>
    /// Generates a cache key from parameters with a specific type for the cache key prefix
    /// </summary>
    /// <param name="parameters">Object containing parameters to hash</param>
    /// <param name="cacheType">The type to use as prefix in the cache key</param>
    /// <param name="identifier">Optional additional identifier</param>
    /// <returns>Cache key in format: TypeName-Hash</returns>
    public static string GenerateCacheKey(this object parameters, Type cacheType, string? identifier = null)
    {
        var keyBuilder = new StringBuilder();
        
        // Add the identifier if provided
        if (!string.IsNullOrEmpty(identifier))
        {
            keyBuilder.Append($"{identifier}-");
        }
        
        // Add all public properties and their values
        var properties = parameters.GetType().GetProperties();
        foreach (var prop in properties.OrderBy(p => p.Name))
        {
            if (prop.CanRead)
            {
                var value = prop.GetValue(parameters);
                var stringValue = value?.ToString() ?? "null";
                keyBuilder.Append($"{prop.Name}:{stringValue}-");
            }
        }
        
        var key = keyBuilder.ToString().TrimEnd('-');
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(key));
        return cacheType.ToCacheKey(Convert.ToBase64String(hash));
    }

    /// <summary>
    /// Generates a cache key from a simple string identifier
    /// </summary>
    /// <typeparam name="T">The entity type for the cache key prefix</typeparam>
    /// <param name="identifier">The identifier string</param>
    /// <returns>Cache key in format: TypeName-Identifier</returns>
    public static string GenerateCacheKey<T>(this string identifier)
    {
        return typeof(T).ToCacheKey(identifier);
    }

    /// <summary>
    /// Generates a cache key from a simple string identifier with a specific type
    /// </summary>
    /// <param name="identifier">The identifier string</param>
    /// <param name="cacheType">The type to use as prefix in the cache key</param>
    /// <returns>Cache key in format: TypeName-Identifier</returns>
    public static string GenerateCacheKey(this string identifier, Type cacheType)
    {
        return cacheType.ToCacheKey(identifier);
    }
}
