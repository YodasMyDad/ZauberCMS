using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Serilog;
using ZauberCMS.Core.Content.Interfaces;
using ZauberCMS.Core.Content.Models;
using ZauberCMS.Core.Data;
using ZauberCMS.Core.Plugins;
using ZauberCMS.Core.Settings;
using ZauberCMS.Core.Shared.Interfaces;
using ZauberCMS.Core.Shared.Models;
using ZauberCMS.Core.Shared.Services;

namespace ZauberCMS.Core.Extensions;

public static class DbContextExtensions
{
    /// <summary>
    /// Filters the entities based on whether their raw Path column contains the specified contentId.
    /// </summary>
    /// <param name="source">The source IQueryable of entities.</param>
    /// <param name="itemId">The ID to look for in the Path column.</param>
    /// <returns>An IQueryable of entities matching the condition.</returns>
    public static IQueryable<T> WherePathLike<T>(this DbSet<T> source, Guid itemId)
        where T : class, IBaseItem
    {
        var tableName = typeof(T) == typeof(Content.Models.Content) ? "ZauberContent" : "ZauberMedia";
        var searchPattern = $"%\"{itemId}\"%";

        // Use parameterized query to prevent SQL injection
#pragma warning disable EF1002
        return source.FromSqlRaw($$"""
                                      SELECT * 
                                      FROM {{tableName}}
                                      WHERE Path LIKE {0}
                                  """, searchPattern);
#pragma warning restore EF1002
    }
    
    public static IQueryable<ContentType> WhereHasCompositionsUsing(this DbSet<ContentType> source, Guid itemId)
    {
        var searchPattern = $"%\"{itemId}\"%";
        
        // Use parameterized query to prevent SQL injection
#pragma warning disable EF1002
        return source.FromSqlRaw($$"""
                                      SELECT * 
                                      FROM ZauberContentTypes
                                      WHERE CompositionIds LIKE {0}
                                  """, searchPattern);
#pragma warning restore EF1002
    }
    
    public static List<Guid> BuildPath<T>(this T entity, IZauberDbContext dbContext, bool isUpdate, IOptions<ZauberSettings> settings)
        where T : class, IBaseItem
    {
        var path = new List<Guid>();
        var urls = new List<string>();
        IBaseItem? currentEntity = entity;

        while (currentEntity != null)
        {
            path.Insert(0, currentEntity.Id);
            if (currentEntity.Url != null) urls.Insert(0, currentEntity.Url);

            var parentItem = currentEntity.ParentId.HasValue
                ? dbContext.Set<T>().FirstOrDefault(e => e.Id == currentEntity.ParentId.Value)
                : null;

            currentEntity = parentItem;
        }

        if (entity is Content.Models.Content)
        {
            if (!isUpdate && settings.Value.EnablePathUrls)
            {
                // New item and path URLs are enabled—generate the URL from the path.
                if (urls.Count > 0) urls.RemoveAt(0); // Remove the root (if applicable)
                entity.Url = string.Join("/", urls);
            }    
        }
        
        return path;
    }

    
    public static string GenerateCacheKey<T>(this IQueryable<T> query, Type cacheType)
    {
        // Get the query string
        var queryString = query.ToQueryString();

        // Generate a SHA256 hash of the query string
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(queryString));

        // Return the cache key by combining the type and the hashed query string
        return cacheType.ToCacheKey(Convert.ToBase64String(hash));
    }

    
    public static IQueryable<T>? ToTyped<T>(this IZauberDbContext context) where T : class
    {
        try
        {
            var dbSet = context.Set<T>();
            return dbSet.AsQueryable();
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"Unable to get {nameof(T)} as DbSet");
        }

        return null;
    }
    
    public static async Task<HandlerResult<T>> SaveChangesAndLog<T>(this IZauberDbContext context, T? entity,
        HandlerResult<T> crudResult, ICacheService cacheService, ExtensionManager extensionManager, CancellationToken cancellationToken)
    {
        try
        {
            var canSave = true;
            var entityState = entity != null ? context.Entry(entity).State : EntityState.Unchanged;
            
            // Find any before save plugins
            if (entity != null)
            {
                var beforeSaves = extensionManager.GetInstances<IBeforeEntitySave<T>>(true);
                foreach (var kvp in beforeSaves.OrderBy(x => x.Value.SortOrder))
                {
                    canSave = kvp.Value.BeforeSave(entity, entityState);
                    if (!canSave)
                    {
                        break;
                    }
                }
            }

            if (canSave)
            {
                var isSaved = await context.SaveChangesAsync(cancellationToken);
                
                // Clear cache after successful save
                cacheService.ClearCachedItemsWithPrefix(typeof(T).Name);
                
                crudResult.Success = true;
                if (entity != null)
                {
                    crudResult.Entity = entity;
                }
                
                // Only warn if we expected changes but got none (e.g., trying to add a new entity but 0 rows affected)
                // Don't warn for Modified/Unchanged states as no changes is normal for already-synced entities
                if (isSaved <= 0 && entityState == EntityState.Added)
                {
                    Log.Warning($"{typeof(T).Name} returned 0 items saved when creating or updating (EntityState was {entityState})");
                }   
                
                // After save plugins
                if (entity != null)
                {
                    var afterSaves = extensionManager.GetInstances<IAfterEntitySave<T>>(true);
                    var shouldReSave = false;

                    foreach (var kvp in afterSaves.OrderBy(x => x.Value.SortOrder))
                    {
                        // Use OR operation so if ANY plugin returns true, we re-save
                        // Pass the original entityState since SaveChangesAsync() marks entities as Unchanged
                        shouldReSave |= kvp.Value.AfterSave(entity, entityState);
                    }

                    // If AfterSave logic updated something, persist it to the DB again
                    if (shouldReSave)
                    {
                        await context.SaveChangesAsync(cancellationToken);
                        // Clear cache again after the re-save
                        cacheService.ClearCachedItemsWithPrefix(typeof(T).Name);
                    }
                }

            }
            else
            {
                crudResult.Success = false;
                crudResult.AddMessage("Save was purposely abandoned", ResultMessageType.Error);
            }
        }
        catch (Exception ex)
        {
            crudResult.Success = false;
            crudResult.AddMessage($"{ex.Message} - {ex.InnerException?.Message}", ResultMessageType.Error);
            Log.Error(ex, $"{typeof(T).Name} not saved using SaveChangesAsync");
        }

        return crudResult;
    }


    /// <summary>
    /// Returns paginated list from a queryable
    /// </summary>
    /// <param name="items"></param>
    /// <param name="pageIndex"></param>
    /// <param name="pageSize"></param>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public static PaginatedList<T> ToPaginatedList<T>(this IQueryable<T> items, int pageIndex, int pageSize)
    {
        return new PaginatedList<T>(items, pageIndex, pageSize);
    }
}