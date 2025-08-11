# Cache Key Generation Refactoring Guide

## Overview

This guide demonstrates how to refactor repetitive cache key generation code across ZauberCMS services using the new extension methods. The goal is to eliminate duplicate code while maintaining the important type-based prefix for cache flushing.

## Problem Analysis

Before refactoring, services had repetitive cache key generation patterns:

1. **Query-based caching**: Manual SHA256 hashing of query strings
2. **Parameter-based caching**: Manual string building and hashing
3. **Simple string caching**: Direct type-based key generation

## Solution: Enhanced Extension Methods

### 1. Query-Based Cache Keys

**Before (repetitive):**
```csharp
private static string GenerateCacheKey(GetContentParameters request, IZauberDbContext dbContext)
{
    var query = BuildQuery(request, dbContext);
    var queryString = query.ToQueryString();
    var hash = SHA256.HashData(Encoding.UTF8.GetBytes(queryString));
    return typeof(Models.Content).ToCacheKey(Convert.ToBase64String(hash));
}
```

**After (elegant):**
```csharp
private static string GenerateCacheKey(GetContentParameters request, IZauberDbContext dbContext)
{
    var query = BuildQuery(request, dbContext);
    return query.GenerateCacheKey<Models.Content>();
}
```

### 2. Parameter-Based Cache Keys

**Before (repetitive):**
```csharp
private static string GenerateGetContentFromRequestCacheKey(GetContentFromRequestParameters request)
{
    var keyBuilder = new StringBuilder();
    keyBuilder.Append($"GetContentFromRequest-");
    keyBuilder.Append($"Url:{request.Url ?? "null"}-");
    keyBuilder.Append($"Slug:{request.Slug ?? "null"}-");
    keyBuilder.Append($"IsRoot:{request.IsRootContent}-");
    keyBuilder.Append($"IncludeChildren:{request.IncludeChildren}");
    var hash = SHA256.HashData(Encoding.UTF8.GetBytes(keyBuilder.ToString()));
    return typeof(Models.Content).ToCacheKey(Convert.ToBase64String(hash));
}
```

**After (elegant):**
```csharp
private static string GenerateGetContentFromRequestCacheKey(GetContentFromRequestParameters request)
{
    return request.GenerateCacheKey<Models.Content>("GetContentFromRequest");
}
```

### 3. Simple String Cache Keys

**Before (repetitive):**
```csharp
var cacheKey = typeof(LanguageDictionary).ToCacheKey("GetCachedAllLanguageDictionaries");
```

**After (elegant):**
```csharp
var cacheKey = "GetCachedAllLanguageDictionaries".GenerateCacheKey<LanguageDictionary>();
```

## Available Extension Methods

### For IQueryable<T>
```csharp
// Generate cache key from query with entity type
query.GenerateCacheKey<Content>()

// Generate cache key from query with specific type
query.GenerateCacheKey(typeof(Content))
```

### For Parameters Object
```csharp
// Generate cache key from parameters with entity type
parameters.GenerateCacheKey<Content>()

// Generate cache key from parameters with specific type
parameters.GenerateCacheKey(typeof(Content))

// Generate cache key with additional identifier
parameters.GenerateCacheKey<Content>("CustomIdentifier")
```

### For Simple Strings
```csharp
// Generate cache key from string with entity type
"identifier".GenerateCacheKey<Content>()

// Generate cache key from string with specific type
"identifier".GenerateCacheKey(typeof(Content))
```

## Refactoring Examples

### ContentService
```csharp
// Before: Manual query hashing
private static string GenerateGetContentCacheKey(GetContentParameters request, IZauberDbContext dbContext)
{
    var query = BuildQuery(request, dbContext);
    var queryString = query.ToQueryString();
    var hash = SHA256.HashData(Encoding.UTF8.GetBytes(queryString));
    return typeof(Models.Content).ToCacheKey(Convert.ToBase64String(hash));
}

// After: Using extension method
private static string GenerateGetContentCacheKey(GetContentParameters request, IZauberDbContext dbContext)
{
    var query = BuildQuery(request, dbContext);
    return query.GenerateCacheKey<Models.Content>();
}
```

### MediaService
```csharp
// Before: Manual parameter hashing
private static string GenerateCacheKey(GetMediaParameters parameters, IZauberDbContext dbContext)
{
    var query = BuildQuery(parameters, dbContext);
    var queryString = query.ToQueryString();
    var hash = SHA256.HashData(Encoding.UTF8.GetBytes(queryString));
    return typeof(Models.Media).ToCacheKey(Convert.ToBase64String(hash));
}

// After: Using extension method
private static string GenerateCacheKey(GetMediaParameters parameters, IZauberDbContext dbContext)
{
    var query = BuildQuery(parameters, dbContext);
    return query.GenerateCacheKey<Models.Media>();
}
```

### LanguageService
```csharp
// Before: Direct type-based key
var cacheKey = typeof(LanguageDictionary).ToCacheKey("GetCachedAllLanguageDictionaries");

// After: Using extension method
var cacheKey = "GetCachedAllLanguageDictionaries".GenerateCacheKey<LanguageDictionary>();
```

## Benefits of Refactoring

1. **Eliminates Repetitive Code**: No more manual SHA256 hashing and Base64 encoding
2. **Maintains Type Safety**: Type-based prefix is preserved for cache flushing
3. **Improves Readability**: Code intent is clearer and more concise
4. **Reduces Errors**: Centralized logic reduces the chance of implementation mistakes
5. **Easier Maintenance**: Changes to cache key logic only need to be made in one place
6. **Consistent Pattern**: All services use the same approach for cache key generation

## Cache Key Format

All generated cache keys maintain the format: `TypeName-Identifier`

- **TypeName**: The entity type name (e.g., "Content", "Media", "User")
- **Identifier**: Either a hash of the query/parameters or a simple string

This format ensures that cache flushing based on entity changes continues to work correctly.

## Migration Checklist

- [ ] Replace manual SHA256 hashing with `query.GenerateCacheKey<T>()`
- [ ] Replace manual parameter string building with `parameters.GenerateCacheKey<T>()`
- [ ] Replace direct type-based keys with `"identifier".GenerateCacheKey<T>()`
- [ ] Remove unused `using System.Security.Cryptography;` statements
- [ ] Remove unused `using System.Text;` statements
- [ ] Test cache functionality to ensure keys are generated correctly
- [ ] Verify cache flushing still works based on entity type names

## Notes

- The existing `DbContextExtensions.GenerateCacheKey` method is preserved for backward compatibility
- All new extension methods are in the `CacheExtensions` class
- The `ToCacheKey` extension methods remain unchanged
- Cache key format and behavior are identical to the previous implementation
