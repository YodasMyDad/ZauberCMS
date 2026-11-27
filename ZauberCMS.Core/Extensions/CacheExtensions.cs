using System.Globalization;
using System.Linq.Expressions;
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
    /// Generates a cache key from an IQueryable by hashing the query string and the related data it loads
    /// </summary>
    /// <typeparam name="T">The entity type for the cache key prefix</typeparam>
    /// <param name="query">The query to hash</param>
    /// <returns>Cache key in format: TypeName-Hash</returns>
    public static string GenerateCacheKey<T>(this IQueryable<T> query) => typeof(T).ToCacheKey(HashQuery(query));

    /// <summary>The query's SQL plus the related data it loads, hashed (used by every query cache key).</summary>
    internal static string HashQuery<T>(IQueryable<T> query)
    {
        // ToQueryString shows only the first statement of a split query, so the includes (Children, roles, …)
        // are added from the expression; without them a query with and without children shared a key (ZA-072)
        var key = new StringBuilder(query.ToQueryString());
        new IncludeCollector(key).Visit(query.Expression);
        return Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(key.ToString())));
    }

    private sealed class IncludeCollector(StringBuilder key) : ExpressionVisitor
    {
        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            if (node.Method.DeclaringType == typeof(EntityFrameworkQueryableExtensions))
            {
                key.Append('\n').Append(node.Method.Name);
                foreach (var argument in node.Arguments.Skip(1))
                {
                    key.Append('|').Append(argument);
                    // A filtered include prints a captured variable as value(Closure).name: add its value
                    new CapturedValueCollector(key).Visit(argument);
                }
            }

            return base.VisitMethodCall(node);
        }
    }

    private sealed class CapturedValueCollector(StringBuilder key) : ExpressionVisitor
    {
        protected override Expression VisitMember(MemberExpression node)
        {
            // The whole chain from the closure (value(Closure).parameters.Ids), not just its first step
            if (!TryEvaluate(node, out var value))
            {
                return base.VisitMember(node);
            }

            key.Append('=').Append(value is System.Collections.IEnumerable items and not string
                ? string.Join(",", items.Cast<object?>())
                : value);
            return node;
        }

        private static bool TryEvaluate(Expression? expression, out object? value)
        {
            switch (expression)
            {
                case ConstantExpression constant:
                    value = constant.Value;
                    return true;
                case MemberExpression member when TryEvaluate(member.Expression, out var owner) ||
                                                  member.Expression == null:
                    owner = member.Expression == null ? null : owner;
                    value = member.Member switch
                    {
                        // A null owner of an instance member: EF would fail on it too; leave the key as it is
                        System.Reflection.FieldInfo field when owner != null || field.IsStatic => field.GetValue(owner),
                        System.Reflection.PropertyInfo property when owner != null || property.GetMethod?.IsStatic == true
                            => property.GetValue(owner),
                        _ => null
                    };
                    return true;
                default:
                    value = null;
                    return false;
            }
        }
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
            keyBuilder.Append(CultureInfo.InvariantCulture, $"{identifier}-");
        }
        
        // Add all public properties and their values
        var properties = parameters.GetType().GetProperties();
        foreach (var prop in properties.OrderBy(p => p.Name))
        {
            if (prop.CanRead)
            {
                var value = prop.GetValue(parameters);
                var stringValue = value?.ToString() ?? "null";
                keyBuilder.Append(CultureInfo.InvariantCulture, $"{prop.Name}:{stringValue}-");
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
            keyBuilder.Append(CultureInfo.InvariantCulture, $"{identifier}-");
        }
        
        // Add all public properties and their values
        var properties = parameters.GetType().GetProperties();
        foreach (var prop in properties.OrderBy(p => p.Name))
        {
            if (prop.CanRead)
            {
                var value = prop.GetValue(parameters);
                var stringValue = value?.ToString() ?? "null";
                keyBuilder.Append(CultureInfo.InvariantCulture, $"{prop.Name}:{stringValue}-");
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
