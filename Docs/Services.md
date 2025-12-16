---
alwaysApply: true
---

# ZauberCMS Services Developer Guide

This guide explains the service architecture, patterns, and best practices for working with ZauberCMS services. Understanding these patterns is crucial for maintaining consistency and avoiding performance issues.

## Table of Contents

1. [Service Architecture Overview](#service-architecture-overview)
2. [Core Service Patterns](#core-service-patterns)
3. [Parameter Classes](#parameter-classes)
4. [Query Building Patterns](#query-building-patterns)
5. [Caching Strategy](#caching-strategy)
6. [ContentService Deep Dive](#contentservice-deep-dive)
7. [Common Pitfalls & Best Practices](#common-pitfalls--best-practices)
8. [Adding New Services](#adding-new-services)

## Service Architecture Overview

ZauberCMS uses a service-oriented architecture with the following core services:

### Registered Services (ZauberSetup.cs)
- **`IContentService`** - Content and ContentType management (most complex)
- **`IContentVersioningService`** - Content versioning and history
- **`IMembershipService`** - User and role management
- **`IMediaService`** - Media management and storage
- **`ILanguageService`** - Multi-language support
- **`ITagService`** - Tagging system
- **`IAuditService`** - Audit logging
- **`ISeoService`** - SEO redirects and metadata
- **`IDataService`** - Global data management
- **`IEmailService`** - Email communications

### Service Dependencies

All services follow a consistent dependency pattern:

```csharp
public class ServiceName(
    IServiceProvider serviceProvider,           // Required: For scoped DbContext access
    ICacheService cacheService,                // Required: For caching
    AuthenticationStateProvider authProvider,  // For user context
    ExtensionManager extensionManager,         // For plugins
    // Additional specific dependencies...
) : IServiceInterface
```

**Critical**: Always inject `IServiceProvider` and create scoped `DbContext` instances:

```csharp
public async Task<T> MethodAsync(Parameters parameters, CancellationToken cancellationToken = default)
{
    using var scope = serviceProvider.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<IZauberDbContext>();
    // ... service logic
}
```

## Core Service Patterns

### 1. Get Single Entity Pattern

```csharp
public async Task<Entity?> GetEntityAsync(GetEntityParameters parameters, CancellationToken cancellationToken = default)
{
    using var scope = serviceProvider.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<IZauberDbContext>();
    var query = BuildQuery(parameters, dbContext);
    var cacheKey = query.GenerateCacheKey<Entity>();
    
    if (parameters.Cached)
    {
        return await cacheService.GetSetCachedItemAsync(cacheKey, 
            async () => await query.FirstOrDefaultAsync(cancellationToken));
    }
    
    return await query.FirstOrDefaultAsync(cancellationToken);
}
```

### 2. Save Entity Pattern

```csharp
public async Task<HandlerResult<Entity>> SaveEntityAsync(SaveEntityParameters parameters, CancellationToken cancellationToken = default)
{
    using var scope = serviceProvider.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<IZauberDbContext>();
    var authState = await authenticationStateProvider.GetAuthenticationStateAsync();
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
    var user = await userManager.GetUserAsync(authState.User);
    var handlerResult = new HandlerResult<Entity>();
    
    // Validation
    if (parameters.Entity == null)
    {
        handlerResult.AddMessage("Entity is null", ResultMessageType.Error);
        return handlerResult;
    }
    
    // Create or Update logic
    var entity = dbContext.Entities.FirstOrDefault(x => x.Id == parameters.Entity.Id);
    var isUpdate = entity != null;
    
    if (entity == null)
    {
        entity = parameters.Entity;
        entity.LastUpdatedById = user!.Id;
        dbContext.Entities.Add(entity);
    }
    else
    {
        parameters.Entity.MapTo(entity);
        entity.LastUpdatedById = user!.Id;
        entity.DateUpdated = DateTime.UtcNow;
    }
    
    // Save with plugins and cache invalidation
    return await dbContext.SaveChangesAndLog(entity, handlerResult, cacheService, extensionManager, cancellationToken);
}
```

## Parameter Classes

Parameter classes encapsulate all options for service methods. They follow consistent patterns:

### Base Parameter Structure

```csharp
public class GetEntityParameters
{
    public bool Cached { get; set; }                    // Enable caching
    public bool AsNoTracking { get; set; } = true;     // EF tracking
    public Guid? Id { get; set; }                       // Primary identifier
    public bool IncludeChildren { get; set; }          // Navigation properties
    public bool IncludeParent { get; set; }            // Navigation properties
}

public class QueryEntityParameters : BaseQueryEntityParameters
{
    public bool Cached { get; set; }
    public List<Guid> Ids { get; set; } = [];
    public int PageIndex { get; set; } = 1;
    public int AmountPerPage { get; set; } = 10;
    public string? SearchTerm { get; set; }
    public Expression<Func<Entity, bool>>? WhereClause { get; set; }
    public Func<IQueryable<Entity>>? Query { get; set; }  // Custom query override
    public GetEntityOrderBy OrderBy { get; set; } = GetEntityOrderBy.DateUpdatedDescending;
}
```

### Critical Parameter Patterns

1. **Always provide defaults** for collection properties: `= []`
2. **Use nullable types** for optional filters: `Guid?`, `string?`
3. **Enable AsNoTracking by default** for read operations: `= true`
4. **Include OrderBy enums** for consistent sorting
5. **Support both WhereClause and Query overrides** for flexibility

## Query Building Patterns

All services use `BuildQuery` methods to construct EF queries. This pattern ensures consistency and performance.

### Standard BuildQuery Pattern

```csharp
private static IQueryable<Entity> BuildQuery(QueryEntityParameters parameters, IZauberDbContext dbContext)
{
    var query = dbContext.Entities.Include(x => x.RelatedEntity).AsQueryable();

    // Custom query override (advanced usage)
    if (parameters.Query != null)
    {
        query = parameters.Query.Invoke();
    }
    else
    {
        // Standard query building
        if (parameters.AsNoTracking)
        {
            query = query.AsNoTracking();
        }

        if (parameters.Ids.Count != 0)
        {
            query = query.Where(x => parameters.Ids.Contains(x.Id));
            parameters.AmountPerPage = parameters.Ids.Count; // Optimization
        }

        if (!string.IsNullOrWhiteSpace(parameters.SearchTerm))
        {
            var searchTermLower = parameters.SearchTerm.ToLower();
            query = query.Where(x => x.Name != null && x.Name.ToLower().Contains(searchTermLower));
        }

        // Add conditional includes
        if (parameters.IncludeChildren)
        {
            query = query.Include(x => x.Children);
        }
    }

    // Custom where clause (always applied)
    if (parameters.WhereClause != null)
    {
        query = query.Where(parameters.WhereClause);
    }

    // Ordering (always at the end)
    query = parameters.OrderBy switch
    {
        GetEntityOrderBy.DateUpdated => query.OrderBy(p => p.DateUpdated),
        GetEntityOrderBy.DateUpdatedDescending => query.OrderByDescending(p => p.DateUpdated),
        GetEntityOrderBy.Name => query.OrderBy(p => p.Name),
        _ => query.OrderByDescending(p => p.DateUpdated)
    };

    return query;
}
```

### Critical Query Building Rules

1. **Always start with base entity and required includes**
2. **Apply AsNoTracking early** for read-only operations
3. **Use AsSplitQuery()** when including multiple collections
4. **Apply filters in order of selectivity** (most selective first)
5. **Always apply ordering last**
6. **Support both Query override and standard building**
7. **Optimize AmountPerPage when using Ids filter**

## ContentService Deep Dive

ContentService is the most complex service, handling Content, ContentTypes, and Domains. Key patterns to understand:

### Complex Query Building

ContentService has multiple `BuildQuery` overloads for different scenarios:

```csharp
// Single content item
private IQueryable<Models.Content> BuildQuery(GetContentParameters request, IZauberDbContext dbContext)
{
    var query = dbContext.Contents
        .Include(x => x.ContentType)
        .Include(x => x.PropertyData)
        .AsSplitQuery()
        .AsQueryable();

    if (!request.IncludeUnpublished)
    {
        query = query.Where(x => x.Published);
    }

    if (request.IncludeChildren)
    {
        query = request.IncludeUnpublished 
            ? query.Include(x => x.Children)
            : query.Include(x => x.Children.Where(c => c.Published));
        query = query.AsSplitQuery();
    }

    return query;
}
```

### Property Data Updates

ContentService handles complex property data synchronization:

```csharp
private static void UpdateContentPropertyValues(IZauberDbContext dbContext, Models.Content content, List<ContentPropertyValue> newPropertyValues)
{
    // Remove deleted items
    var deletedItems = content.PropertyData.Where(epv => newPropertyValues.All(npv => npv.Id != epv.Id)).ToList();
    foreach (var deletedItem in deletedItems)
    {
        dbContext.ContentPropertyValues.Remove(deletedItem);
    }

    // Add or update items
    foreach (var newPropertyValue in newPropertyValues)
    {
        var existingPropertyValue = content.PropertyData.FirstOrDefault(epv => epv.Id == newPropertyValue.Id);
        if (existingPropertyValue == null)
        {
            dbContext.ContentPropertyValues.Add(newPropertyValue);
        }
        else
        {
            newPropertyValue.MapTo(existingPropertyValue);
        }
    }
}
```

## Common Pitfalls & Best Practices

### ❌ Don't Do This

```csharp
// DON'T: Inject DbContext directly
public class BadService(IZauberDbContext dbContext) // ❌

// DON'T: Forget to clear cache on updates
await dbContext.SaveChangesAsync(); // ❌ No cache clearing

// DON'T: Use tracking for read-only operations
var query = dbContext.Contents.AsQueryable(); // ❌ Tracking enabled

// DON'T: Forget pagination limits
var contents = await contentService.QueryContentAsync(new QueryContentParameters 
{ 
    AmountPerPage = int.MaxValue // ❌ Can cause memory issues
});
```

### ✅ Do This Instead

```csharp
// ✅ Use IServiceProvider pattern
public class GoodService(IServiceProvider serviceProvider)

// ✅ Create scoped DbContext per operation
using var scope = serviceProvider.CreateScope();
var dbContext = scope.ServiceProvider.GetRequiredService<IZauberDbContext>();

// ✅ Use SaveChangesAndLog for automatic cache clearing
await dbContext.SaveChangesAndLog(entity, result, cacheService, extensionManager, cancellationToken);

// ✅ Use AsNoTracking for read operations
var query = dbContext.Contents.AsNoTracking().AsQueryable();

// ✅ Set reasonable pagination limits
var contents = await contentService.QueryContentAsync(new QueryContentParameters 
{ 
    AmountPerPage = Math.Min(requestedAmount, 100) // ✅ Limit maximum
});
```

### Performance Best Practices

1. **Always use AsNoTracking** for read-only operations
2. **Use AsSplitQuery()** when including multiple collections
3. **Apply most selective filters first** in BuildQuery methods
4. **Limit AmountPerPage** to reasonable values (typically ≤ 100)
5. **Cache expensive queries** with appropriate expiration
6. **Use projection** for large datasets: `.Select(x => new { x.Id, x.Name })`

### Caching Guidelines

1. **Cache read-heavy operations** (Get/Query methods)
2. **Don't cache write operations** (Save/Delete methods)
3. **Use appropriate cache expiration** (5-60 minutes typically)
4. **Clear cache on related updates** (handled by SaveChangesAndLog)

## Key Things to Check When Modifying Services

### When Adding New Query Parameters

1. **Update parameter classes** with appropriate defaults
2. **Add parameter handling** in BuildQuery methods
3. **Test with and without the parameter** to ensure backward compatibility
4. **Consider performance impact** of new filters/includes
5. **Update cache keys** if the parameter affects results

### When Modifying Query Logic

1. **Test all parameter combinations** that could be affected
2. **Verify AsNoTracking behavior** remains consistent
3. **Check AsSplitQuery usage** for multiple includes
4. **Ensure ordering is applied last** in BuildQuery
5. **Test pagination** with new filters

### When Adding New Entities

1. **Follow established parameter patterns**
2. **Implement all CRUD operations** consistently
3. **Add appropriate caching** to read operations
4. **Include audit logging** for changes
5. **Register service** in ZauberSetup.cs (lines 88-98)
6. **Add DbSet** to IZauberDbContext and ZauberDbContextBase

Remember: ContentService serves as the canonical example. When in doubt, follow its patterns for consistency with the rest of the codebase.

## Adding New Services

When adding a new service to ZauberCMS, follow this complete checklist:

### 1. Create Interface and Implementation

```csharp
// ZauberCMS.Core/NewFeature/Interfaces/INewFeatureService.cs
public interface INewFeatureService
{
    Task<NewEntity?> GetNewEntityAsync(GetNewEntityParameters parameters, CancellationToken cancellationToken = default);
    Task<PaginatedList<NewEntity>> QueryNewEntitiesAsync(QueryNewEntityParameters parameters, CancellationToken cancellationToken = default);
    Task<HandlerResult<NewEntity>> SaveNewEntityAsync(SaveNewEntityParameters parameters, CancellationToken cancellationToken = default);
    Task<HandlerResult<NewEntity>> DeleteNewEntityAsync(DeleteNewEntityParameters parameters, CancellationToken cancellationToken = default);
}

// ZauberCMS.Core/NewFeature/Services/NewFeatureService.cs
public class NewFeatureService(
    IServiceProvider serviceProvider,
    ICacheService cacheService,
    AuthenticationStateProvider authenticationStateProvider,
    ExtensionManager extensionManager)
    : INewFeatureService
{
    // Follow established patterns from ContentService/MediaService
}
```

### 2. Register in Dependency Injection

Add to `ZauberSetup.cs` in the service registration section (lines 88-98):

```csharp
// Register Service Interfaces and Implementations
builder.Services.AddScoped<IContentService, ContentService>();
builder.Services.AddScoped<IContentVersioningService, ContentVersioningService>();
builder.Services.AddScoped<IMembershipService, MembershipService>();
builder.Services.AddScoped<IMediaService, MediaService>();
builder.Services.AddScoped<ILanguageService, LanguageService>();
builder.Services.AddScoped<ITagService, TagService>();
builder.Services.AddScoped<IAuditService, AuditService>();
builder.Services.AddScoped<ISeoService, SeoService>();
builder.Services.AddScoped<IDataService, DataService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<INewFeatureService, NewFeatureService>(); // Add your new service here
```

**Important**: All services are registered as `Scoped` to ensure proper DbContext lifecycle management.

### 3. Update Database Context

If your service manages entities, add the DbSet:

```csharp
// IZauberDbContext.cs
DbSet<NewEntity> NewEntities { get; }

// ZauberDbContextBase.cs
public DbSet<NewEntity> NewEntities => Set<NewEntity>();
```

This ensures your new service follows ZauberCMS patterns and integrates properly with the DI container.# ZauberCMS Services Developer Guide

This guide explains the service architecture, patterns, and best practices for working with ZauberCMS services. Understanding these patterns is crucial for maintaining consistency and avoiding performance issues.

## Table of Contents

1. [Service Architecture Overview](#service-architecture-overview)
2. [Core Service Patterns](#core-service-patterns)
3. [Parameter Classes](#parameter-classes)
4. [Query Building Patterns](#query-building-patterns)
5. [Caching Strategy](#caching-strategy)
6. [ContentService Deep Dive](#contentservice-deep-dive)
7. [Common Pitfalls & Best Practices](#common-pitfalls--best-practices)
8. [Adding New Services](#adding-new-services)

## Service Architecture Overview

ZauberCMS uses a service-oriented architecture with the following core services:

### Registered Services (ZauberSetup.cs)
- **`IContentService`** - Content and ContentType management (most complex)
- **`IContentVersioningService`** - Content versioning and history
- **`IMembershipService`** - User and role management
- **`IMediaService`** - Media management and storage
- **`ILanguageService`** - Multi-language support
- **`ITagService`** - Tagging system
- **`IAuditService`** - Audit logging
- **`ISeoService`** - SEO redirects and metadata
- **`IDataService`** - Global data management
- **`IEmailService`** - Email communications

### Service Dependencies

All services follow a consistent dependency pattern:

```csharp
public class ServiceName(
    IServiceProvider serviceProvider,           // Required: For scoped DbContext access
    ICacheService cacheService,                // Required: For caching
    AuthenticationStateProvider authProvider,  // For user context
    ExtensionManager extensionManager,         // For plugins
    // Additional specific dependencies...
) : IServiceInterface
```

**Critical**: Always inject `IServiceProvider` and create scoped `DbContext` instances:

```csharp
public async Task<T> MethodAsync(Parameters parameters, CancellationToken cancellationToken = default)
{
    using var scope = serviceProvider.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<IZauberDbContext>();
    // ... service logic
}
```

## Core Service Patterns

### 1. Get Single Entity Pattern

```csharp
public async Task<Entity?> GetEntityAsync(GetEntityParameters parameters, CancellationToken cancellationToken = default)
{
    using var scope = serviceProvider.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<IZauberDbContext>();
    var query = BuildQuery(parameters, dbContext);
    var cacheKey = query.GenerateCacheKey<Entity>();
    
    if (parameters.Cached)
    {
        return await cacheService.GetSetCachedItemAsync(cacheKey, 
            async () => await query.FirstOrDefaultAsync(cancellationToken));
    }
    
    return await query.FirstOrDefaultAsync(cancellationToken);
}
```

### 2. Save Entity Pattern

```csharp
public async Task<HandlerResult<Entity>> SaveEntityAsync(SaveEntityParameters parameters, CancellationToken cancellationToken = default)
{
    using var scope = serviceProvider.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<IZauberDbContext>();
    var authState = await authenticationStateProvider.GetAuthenticationStateAsync();
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
    var user = await userManager.GetUserAsync(authState.User);
    var handlerResult = new HandlerResult<Entity>();
    
    // Validation
    if (parameters.Entity == null)
    {
        handlerResult.AddMessage("Entity is null", ResultMessageType.Error);
        return handlerResult;
    }
    
    // Create or Update logic
    var entity = dbContext.Entities.FirstOrDefault(x => x.Id == parameters.Entity.Id);
    var isUpdate = entity != null;
    
    if (entity == null)
    {
        entity = parameters.Entity;
        entity.LastUpdatedById = user!.Id;
        dbContext.Entities.Add(entity);
    }
    else
    {
        parameters.Entity.MapTo(entity);
        entity.LastUpdatedById = user!.Id;
        entity.DateUpdated = DateTime.UtcNow;
    }
    
    // Save with plugins and cache invalidation
    return await dbContext.SaveChangesAndLog(entity, handlerResult, cacheService, extensionManager, cancellationToken);
}
```

## Parameter Classes

Parameter classes encapsulate all options for service methods. They follow consistent patterns:

### Base Parameter Structure

```csharp
public class GetEntityParameters
{
    public bool Cached { get; set; }                    // Enable caching
    public bool AsNoTracking { get; set; } = true;     // EF tracking
    public Guid? Id { get; set; }                       // Primary identifier
    public bool IncludeChildren { get; set; }          // Navigation properties
    public bool IncludeParent { get; set; }            // Navigation properties
}

public class QueryEntityParameters : BaseQueryEntityParameters
{
    public bool Cached { get; set; }
    public List<Guid> Ids { get; set; } = [];
    public int PageIndex { get; set; } = 1;
    public int AmountPerPage { get; set; } = 10;
    public string? SearchTerm { get; set; }
    public Expression<Func<Entity, bool>>? WhereClause { get; set; }
    public Func<IQueryable<Entity>>? Query { get; set; }  // Custom query override
    public GetEntityOrderBy OrderBy { get; set; } = GetEntityOrderBy.DateUpdatedDescending;
}
```

### Critical Parameter Patterns

1. **Always provide defaults** for collection properties: `= []`
2. **Use nullable types** for optional filters: `Guid?`, `string?`
3. **Enable AsNoTracking by default** for read operations: `= true`
4. **Include OrderBy enums** for consistent sorting
5. **Support both WhereClause and Query overrides** for flexibility

## Query Building Patterns

All services use `BuildQuery` methods to construct EF queries. This pattern ensures consistency and performance.

### Standard BuildQuery Pattern

```csharp
private static IQueryable<Entity> BuildQuery(QueryEntityParameters parameters, IZauberDbContext dbContext)
{
    var query = dbContext.Entities.Include(x => x.RelatedEntity).AsQueryable();

    // Custom query override (advanced usage)
    if (parameters.Query != null)
    {
        query = parameters.Query.Invoke();
    }
    else
    {
        // Standard query building
        if (parameters.AsNoTracking)
        {
            query = query.AsNoTracking();
        }

        if (parameters.Ids.Count != 0)
        {
            query = query.Where(x => parameters.Ids.Contains(x.Id));
            parameters.AmountPerPage = parameters.Ids.Count; // Optimization
        }

        if (!string.IsNullOrWhiteSpace(parameters.SearchTerm))
        {
            var searchTermLower = parameters.SearchTerm.ToLower();
            query = query.Where(x => x.Name != null && x.Name.ToLower().Contains(searchTermLower));
        }

        // Add conditional includes
        if (parameters.IncludeChildren)
        {
            query = query.Include(x => x.Children);
        }
    }

    // Custom where clause (always applied)
    if (parameters.WhereClause != null)
    {
        query = query.Where(parameters.WhereClause);
    }

    // Ordering (always at the end)
    query = parameters.OrderBy switch
    {
        GetEntityOrderBy.DateUpdated => query.OrderBy(p => p.DateUpdated),
        GetEntityOrderBy.DateUpdatedDescending => query.OrderByDescending(p => p.DateUpdated),
        GetEntityOrderBy.Name => query.OrderBy(p => p.Name),
        _ => query.OrderByDescending(p => p.DateUpdated)
    };

    return query;
}
```

### Critical Query Building Rules

1. **Always start with base entity and required includes**
2. **Apply AsNoTracking early** for read-only operations
3. **Use AsSplitQuery()** when including multiple collections
4. **Apply filters in order of selectivity** (most selective first)
5. **Always apply ordering last**
6. **Support both Query override and standard building**
7. **Optimize AmountPerPage when using Ids filter**

## ContentService Deep Dive

ContentService is the most complex service, handling Content, ContentTypes, and Domains. Key patterns to understand:

### Complex Query Building

ContentService has multiple `BuildQuery` overloads for different scenarios:

```csharp
// Single content item
private IQueryable<Models.Content> BuildQuery(GetContentParameters request, IZauberDbContext dbContext)
{
    var query = dbContext.Contents
        .Include(x => x.ContentType)
        .Include(x => x.PropertyData)
        .AsSplitQuery()
        .AsQueryable();

    if (!request.IncludeUnpublished)
    {
        query = query.Where(x => x.Published);
    }

    if (request.IncludeChildren)
    {
        query = request.IncludeUnpublished 
            ? query.Include(x => x.Children)
            : query.Include(x => x.Children.Where(c => c.Published));
        query = query.AsSplitQuery();
    }

    return query;
}
```

### Property Data Updates

ContentService handles complex property data synchronization:

```csharp
private static void UpdateContentPropertyValues(IZauberDbContext dbContext, Models.Content content, List<ContentPropertyValue> newPropertyValues)
{
    // Remove deleted items
    var deletedItems = content.PropertyData.Where(epv => newPropertyValues.All(npv => npv.Id != epv.Id)).ToList();
    foreach (var deletedItem in deletedItems)
    {
        dbContext.ContentPropertyValues.Remove(deletedItem);
    }

    // Add or update items
    foreach (var newPropertyValue in newPropertyValues)
    {
        var existingPropertyValue = content.PropertyData.FirstOrDefault(epv => epv.Id == newPropertyValue.Id);
        if (existingPropertyValue == null)
        {
            dbContext.ContentPropertyValues.Add(newPropertyValue);
        }
        else
        {
            newPropertyValue.MapTo(existingPropertyValue);
        }
    }
}
```

## Common Pitfalls & Best Practices

### ❌ Don't Do This

```csharp
// DON'T: Inject DbContext directly
public class BadService(IZauberDbContext dbContext) // ❌

// DON'T: Forget to clear cache on updates
await dbContext.SaveChangesAsync(); // ❌ No cache clearing

// DON'T: Use tracking for read-only operations
var query = dbContext.Contents.AsQueryable(); // ❌ Tracking enabled

// DON'T: Forget pagination limits
var contents = await contentService.QueryContentAsync(new QueryContentParameters 
{ 
    AmountPerPage = int.MaxValue // ❌ Can cause memory issues
});
```

### ✅ Do This Instead

```csharp
// ✅ Use IServiceProvider pattern
public class GoodService(IServiceProvider serviceProvider)

// ✅ Create scoped DbContext per operation
using var scope = serviceProvider.CreateScope();
var dbContext = scope.ServiceProvider.GetRequiredService<IZauberDbContext>();

// ✅ Use SaveChangesAndLog for automatic cache clearing
await dbContext.SaveChangesAndLog(entity, result, cacheService, extensionManager, cancellationToken);

// ✅ Use AsNoTracking for read operations
var query = dbContext.Contents.AsNoTracking().AsQueryable();

// ✅ Set reasonable pagination limits
var contents = await contentService.QueryContentAsync(new QueryContentParameters 
{ 
    AmountPerPage = Math.Min(requestedAmount, 100) // ✅ Limit maximum
});
```

### Performance Best Practices

1. **Always use AsNoTracking** for read-only operations
2. **Use AsSplitQuery()** when including multiple collections
3. **Apply most selective filters first** in BuildQuery methods
4. **Limit AmountPerPage** to reasonable values (typically ≤ 100)
5. **Cache expensive queries** with appropriate expiration
6. **Use projection** for large datasets: `.Select(x => new { x.Id, x.Name })`

### Caching Guidelines

1. **Cache read-heavy operations** (Get/Query methods)
2. **Don't cache write operations** (Save/Delete methods)
3. **Use appropriate cache expiration** (5-60 minutes typically)
4. **Clear cache on related updates** (handled by SaveChangesAndLog)

## Key Things to Check When Modifying Services

### When Adding New Query Parameters

1. **Update parameter classes** with appropriate defaults
2. **Add parameter handling** in BuildQuery methods
3. **Test with and without the parameter** to ensure backward compatibility
4. **Consider performance impact** of new filters/includes
5. **Update cache keys** if the parameter affects results

### When Modifying Query Logic

1. **Test all parameter combinations** that could be affected
2. **Verify AsNoTracking behavior** remains consistent
3. **Check AsSplitQuery usage** for multiple includes
4. **Ensure ordering is applied last** in BuildQuery
5. **Test pagination** with new filters

### When Adding New Entities

1. **Follow established parameter patterns**
2. **Implement all CRUD operations** consistently
3. **Add appropriate caching** to read operations
4. **Include audit logging** for changes
5. **Register service** in ZauberSetup.cs (lines 88-98)
6. **Add DbSet** to IZauberDbContext and ZauberDbContextBase

Remember: ContentService serves as the canonical example. When in doubt, follow its patterns for consistency with the rest of the codebase.

## Adding New Services

When adding a new service to ZauberCMS, follow this complete checklist:

### 1. Create Interface and Implementation

```csharp
// ZauberCMS.Core/NewFeature/Interfaces/INewFeatureService.cs
public interface INewFeatureService
{
    Task<NewEntity?> GetNewEntityAsync(GetNewEntityParameters parameters, CancellationToken cancellationToken = default);
    Task<PaginatedList<NewEntity>> QueryNewEntitiesAsync(QueryNewEntityParameters parameters, CancellationToken cancellationToken = default);
    Task<HandlerResult<NewEntity>> SaveNewEntityAsync(SaveNewEntityParameters parameters, CancellationToken cancellationToken = default);
    Task<HandlerResult<NewEntity>> DeleteNewEntityAsync(DeleteNewEntityParameters parameters, CancellationToken cancellationToken = default);
}

// ZauberCMS.Core/NewFeature/Services/NewFeatureService.cs
public class NewFeatureService(
    IServiceProvider serviceProvider,
    ICacheService cacheService,
    AuthenticationStateProvider authenticationStateProvider,
    ExtensionManager extensionManager)
    : INewFeatureService
{
    // Follow established patterns from ContentService/MediaService
}
```

### 2. Register in Dependency Injection

Add to `ZauberSetup.cs` in the service registration section (lines 88-98):

```csharp
// Register Service Interfaces and Implementations
builder.Services.AddScoped<IContentService, ContentService>();
builder.Services.AddScoped<IContentVersioningService, ContentVersioningService>();
builder.Services.AddScoped<IMembershipService, MembershipService>();
builder.Services.AddScoped<IMediaService, MediaService>();
builder.Services.AddScoped<ILanguageService, LanguageService>();
builder.Services.AddScoped<ITagService, TagService>();
builder.Services.AddScoped<IAuditService, AuditService>();
builder.Services.AddScoped<ISeoService, SeoService>();
builder.Services.AddScoped<IDataService, DataService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<INewFeatureService, NewFeatureService>(); // Add your new service here
```

**Important**: All services are registered as `Scoped` to ensure proper DbContext lifecycle management.

### 3. Update Database Context

If your service manages entities, add the DbSet:

```csharp
// IZauberDbContext.cs
DbSet<NewEntity> NewEntities { get; }

// ZauberDbContextBase.cs
public DbSet<NewEntity> NewEntities => Set<NewEntity>();
```

This ensures your new service follows ZauberCMS patterns and integrates properly with the DI container.