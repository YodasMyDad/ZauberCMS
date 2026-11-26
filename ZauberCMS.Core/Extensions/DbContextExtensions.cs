using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage;
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
        => source.WhereJsonColumnContains(nameof(IBaseItem.Path), itemId);

    /// <summary>Content types whose <see cref="ContentType.CompositionIds"/> contain <paramref name="itemId"/>.</summary>
    public static IQueryable<ContentType> WhereHasCompositionsUsing(this DbSet<ContentType> source, Guid itemId)
        => source.WhereJsonColumnContains(nameof(ContentType.CompositionIds), itemId);

    // The column holds a JSON array of GUIDs, which LINQ can't look into, so this is raw SQL. Table and column names
    // come from the model and are quoted by the provider: unquoted, PostgreSQL folds them to lower case and the
    // query fails (ZA-007). The value is a parameter.
    private static IQueryable<T> WhereJsonColumnContains<T>(this DbSet<T> source, string propertyName, Guid itemId)
        where T : class
    {
        var entityType = source.EntityType;
        var tableName = entityType.GetTableName()!;
        var schema = entityType.GetSchema();
        var columnName = entityType.FindProperty(propertyName)!
            .GetColumnName(StoreObjectIdentifier.Table(tableName, schema))!;
        var sql = source.GetService<ISqlGenerationHelper>();

#pragma warning disable EF1002 // identifiers come from the model, not from input
        return source.FromSqlRaw(
            $"SELECT * FROM {sql.DelimitIdentifier(tableName, schema)} WHERE {sql.DelimitIdentifier(columnName)} LIKE {{0}}",
            $"%\"{itemId}\"%");
#pragma warning restore EF1002
    }
    
    public static List<Guid> BuildPath<T>(this T entity, IZauberDbContext dbContext, bool isUpdate, IOptions<ZauberSettings> settings)
        where T : class, IBaseItem
    {
        var path = new List<Guid>();
        List<Guid>? ancestorIds = null;

        if (entity.ParentId.HasValue)
        {
            // Single round-trip: fetch parent's already-stored Path and append the current id.
            // Each ancestor's Path is kept correct by this method on every save, so we can
            // reuse it instead of walking the chain one entity at a time.
            var parent = dbContext.Set<T>()
                .AsNoTracking()
                .Where(e => e.Id == entity.ParentId.Value)
                .Select(e => new { e.Path })
                .FirstOrDefault();

            if (parent != null && parent.Path.Count > 0)
            {
                path.AddRange(parent.Path);
                ancestorIds = parent.Path;
            }
        }

        path.Add(entity.Id);

        // Blocks (nested content) have no URL (ZA-067)
        if (entity is Content.Models.Content { IsNestedContent: false } && !isUpdate && settings.Value.EnablePathUrls)
        {
            // Build the URL from ancestor URLs + current Url, dropping the root segment to
            // mirror the original behaviour. One extra query, regardless of depth.
            var orderedUrls = new List<string>();

            if (ancestorIds is { Count: > 0 })
            {
                var ancestorUrls = dbContext.Set<T>()
                    .AsNoTracking()
                    .Where(e => ancestorIds.Contains(e.Id) && e.Url != null)
                    .Select(e => new { e.Id, e.Url })
                    .ToList();

                orderedUrls.AddRange(ancestorIds
                    .Select(id => ancestorUrls.FirstOrDefault(a => a.Id == id)?.Url)
                    .Where(u => u != null)!);
            }

            if (entity.Url != null) orderedUrls.Add(entity.Url);
            if (orderedUrls.Count > 0) orderedUrls.RemoveAt(0); // Remove the root (if applicable)
            entity.Url = string.Join("/", orderedUrls);
        }

        return path;
    }

    
    /// <summary>Cache key for a query with <paramref name="cacheType"/> as its prefix (SQL and includes, hashed).</summary>
    public static string GenerateCacheKey<T>(this IQueryable<T> query, Type cacheType) =>
        cacheType.ToCacheKey(CacheExtensions.HashQuery(query));

    
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
    /// Saves every pending change in one <c>SaveChanges</c>, running the <see cref="IBeforeEntitySave{T}"/> and
    /// <see cref="IAfterEntitySave{T}"/> hooks for each of <paramref name="entities"/> with its own state (added,
    /// modified, deleted). Any before-save hook can abandon the whole save. Use it inside a transaction when the
    /// after-save hooks may save again. A concurrency conflict comes back as a failed result with a message for the user.
    /// </summary>
    /// <param name="entities">The entities the save is about; the hooks run for these.</param>
    /// <param name="resultEntity">Set as the result's entity on success.</param>
    public static async Task<HandlerResult<T>> SaveChangesAndLog<T>(this IZauberDbContext context,
        IReadOnlyCollection<T> entities, T? resultEntity, HandlerResult<T> crudResult, ICacheService cacheService,
        ExtensionManager extensionManager, CancellationToken cancellationToken) where T : class
    {
        try
        {
            var states = entities.Select(e => (Entity: e, State: context.Entry(e).State)).ToList();
            var beforeSaves = extensionManager.GetInstances<IBeforeEntitySave<T>>(true).Values
                .OrderBy(x => x.SortOrder).ToList();
            foreach (var (entity, state) in states)
            {
                foreach (var hook in beforeSaves)
                {
                    if (!hook.BeforeSave(entity, state))
                    {
                        crudResult.Success = false;
                        crudResult.AddMessage("Save was purposely abandoned", ResultMessageType.Error);
                        return crudResult;
                    }
                }
            }

            await context.SaveChangesAsync(cancellationToken);
            cacheService.ClearCachedItemsWithPrefix(typeof(T).Name);
            crudResult.Success = true;
            if (resultEntity != null)
            {
                crudResult.Entity = resultEntity;
            }

            var afterSaves = extensionManager.GetInstances<IAfterEntitySave<T>>(true).Values
                .OrderBy(x => x.SortOrder).ToList();
            var shouldReSave = false;
            foreach (var (entity, state) in states)
            {
                foreach (var hook in afterSaves)
                {
                    shouldReSave |= hook.AfterSave(entity, state);
                }
            }

            if (shouldReSave)
            {
                await context.SaveChangesAsync(cancellationToken);
                cacheService.ClearCachedItemsWithPrefix(typeof(T).Name);
            }
        }
        catch (DbUpdateConcurrencyException ex)
        {
            crudResult.Success = false;
            crudResult.AddMessage(
                $"{typeof(T).Name} was changed by someone else since it was opened. Reload it and make the changes again.",
                ResultMessageType.Error);
            Log.Warning(ex, "{Type} not saved: concurrency conflict", typeof(T).Name);
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