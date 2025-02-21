using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Serilog;
using ZauberCMS.Core.Data;
using ZauberCMS.Core.Plugins;
using ZauberCMS.Core.Shared.Interfaces;
using ZauberCMS.Core.Shared.Models;
using ZauberCMS.Core.Shared.Services;

namespace ZauberCMS.Core.Extensions;

public static class DbContextExtensions
{
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