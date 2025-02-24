using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Serilog;
using ZauberCMS.Core.Content.Interfaces;
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

        // Use EF.Functions.Like to avoid in-memory computation
#pragma warning disable EF1002
        return source.FromSqlRaw($"""
                                      SELECT * 
                                      FROM {tableName}
                                      WHERE Path LIKE '%"{itemId}"%'
                                  """);
#pragma warning restore EF1002
    }
    
    public static List<Guid> BuildPath<T>(this T entity, ZauberDbContext dbContext, bool isUpdate, IOptions<ZauberSettings> settings)
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

    
    public static IQueryable<T>? ToTyped<T>(this ZauberDbContext context) where T : class
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
    
    public static async Task<HandlerResult<T>> SaveChangesAndLog<T>(this ZauberDbContext context, T? entity,
        HandlerResult<T> crudResult, ICacheService cacheService, ExtensionManager extensionManager, CancellationToken cancellationToken)
    {
        try
        {
            cacheService.ClearCachedItemsWithPrefix(typeof(T).Name);
            
            var canSave = true;
            // Find any before save plugins
            if (entity != null)
            {
                var beforeSaves = extensionManager.GetInstances<IBeforeEntitySave>(true);
                foreach (var kvp in beforeSaves.Where(x => x.Value.EntityType == typeof(T)))   
                {
                    canSave = kvp.Value.BeforeSave(entity, context.Entry(entity).State);
                    if (!canSave)
                    {
                        break;
                    }
                }
            }

            if (canSave)
            {
                var isSaved = await context.SaveChangesAsync(cancellationToken);
                crudResult.Success = true;
                if (entity != null)
                {
                    crudResult.Entity = entity;
                }
                if (isSaved <= 0)
                {
                    Log.Warning($"{typeof(T).Name} returned 0 items saved when creating or updating");
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