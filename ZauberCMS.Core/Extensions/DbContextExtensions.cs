using System.Collections.Concurrent;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
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
    
    /// <summary>
    /// The entity's <c>Path</c> (its ancestors' ids and its own) from the parent's stored path, and for a new page with
    /// <c>EnablePathUrls</c> its URL from the ancestors' URLs. Two queries at most, whatever the depth.
    /// </summary>
    public static async Task<List<Guid>> BuildPathAsync<T>(this T entity, IZauberDbContext dbContext, bool isUpdate,
        IOptions<ZauberSettings> settings, CancellationToken cancellationToken = default)
        where T : class, IBaseItem
    {
        var path = new List<Guid>();
        List<Guid>? ancestorIds = null;

        if (entity.ParentId.HasValue)
        {
            // Single round-trip: fetch parent's already-stored Path and append the current id.
            // Each ancestor's Path is kept correct by this method on every save, so we can
            // reuse it instead of walking the chain one entity at a time.
            var parent = await dbContext.Set<T>()
                .AsNoTracking()
                .Where(e => e.Id == entity.ParentId.Value)
                .Select(e => new { e.Path })
                .FirstOrDefaultAsync(cancellationToken);

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
                var ancestorUrls = await dbContext.Set<T>()
                    .AsNoTracking()
                    .Where(e => ancestorIds.Contains(e.Id) && e.Url != null)
                    .Select(e => new { e.Id, e.Url })
                    .ToListAsync(cancellationToken);

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

    /// <summary>Blocks on two queries; use <see cref="BuildPathAsync{T}"/> (ZA-082).</summary>
    [Obsolete("Use BuildPathAsync. This blocks the thread on its queries.")]
    public static List<Guid> BuildPath<T>(this T entity, IZauberDbContext dbContext, bool isUpdate, IOptions<ZauberSettings> settings)
        where T : class, IBaseItem
        // Off the caller's context, so a wait from a Blazor circuit can't deadlock on the continuation
        => Task.Run(() => entity.BuildPathAsync(dbContext, isUpdate, settings)).GetAwaiter().GetResult();

    
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
    
    /// <summary>
    /// Saves every pending change in one <c>SaveChanges</c> and runs the save hooks (Roadmap D28): the
    /// <see cref="IBeforeEntitySave{T}"/> hooks of every row the save writes, of any entity type, each with its own
    /// state (added, modified, deleted), then the save, then the <see cref="IAfterEntitySave{T}"/> hooks, and the
    /// <see cref="IAfterEntityCommit{T}"/> hooks once the change is committed: at once without a transaction, at the
    /// commit of the caller's transaction, never when it rolls back (D62). Any
    /// before-save hook abandons the whole save, so nothing is written. <paramref name="entity"/> is the item the
    /// save is about: its hooks run even when it has no changes. Clears the cache prefix of <typeparamref name="T"/>
    /// and of each type written. A concurrency conflict comes back as a failed result with a message for the user.
    /// </summary>
    public static Task<HandlerResult<T>> SaveChangesAndLog<T>(this IZauberDbContext context, T? entity,
        HandlerResult<T> crudResult, ICacheService cacheService, ExtensionManager extensionManager,
        CancellationToken cancellationToken = default) =>
        SaveWithHooksAsync(context, entity == null ? [] : [entity], entity, crudResult, cacheService,
            extensionManager, cancellationToken);

    /// <summary>
    /// Saves every pending change in one <c>SaveChanges</c> and runs the save hooks for every row it writes, as the
    /// single-entity overload does; the hooks of <paramref name="entities"/> run first, and even when they have no
    /// changes. Any before-save hook abandons the whole save. Use it inside a transaction when the after-save hooks
    /// may save again; the after-commit hooks then run when that transaction commits. A concurrency conflict comes
    /// back as a failed result with a message for the user.
    /// </summary>
    /// <param name="entities">The entities the save is about.</param>
    /// <param name="resultEntity">Set as the result's entity on success.</param>
    public static Task<HandlerResult<T>> SaveChangesAndLog<T>(this IZauberDbContext context,
        IReadOnlyCollection<T> entities, T? resultEntity, HandlerResult<T> crudResult, ICacheService cacheService,
        ExtensionManager extensionManager, CancellationToken cancellationToken = default) where T : class =>
        SaveWithHooksAsync(context, [.. entities], resultEntity, crudResult, cacheService, extensionManager,
            cancellationToken);

    private static async Task<HandlerResult<T>> SaveWithHooksAsync<T>(IZauberDbContext context,
        IReadOnlyList<object> named, T? resultEntity, HandlerResult<T> crudResult, ICacheService cacheService,
        ExtensionManager extensionManager, CancellationToken cancellationToken)
    {
        // The after-commit calls of a save that SaveChanges committed itself (no transaction open): they run even
        // when an after-save hook's second save then fails, since the first is stored
        List<AfterCommitHooks.Call>? committed = null;
        try
        {
            var rows = RowsToSave(context, named);
            var hooks = new Dictionary<Type, SaveHooks>();
            foreach (var (row, type, state) in rows)
            {
                foreach (var beforeSave in HooksFor(type, extensionManager, hooks).Before)
                {
                    if (!beforeSave.Run(row, state))
                    {
                        Log.Information("Save of {Type} abandoned by the save hook {Hook}", type.Name, beforeSave.Name);
                        crudResult.Success = false;
                        crudResult.AddMessage("Save was purposely abandoned", ResultMessageType.Error);
                        return crudResult;
                    }
                }
            }

            await context.SaveChangesAsync(cancellationToken);
            var written = rows.Select(r => r.Type.Name).Append(typeof(T).Name).Distinct().ToList();
            ClearPrefixes(cacheService, written);
            crudResult.Success = true;
            if (resultEntity != null)
            {
                crudResult.Entity = resultEntity;
            }

            // Built now, while the states are the ones saved (D62)
            var afterCommit = new List<AfterCommitHooks.Call>();
            foreach (var (row, type, state) in rows)
            {
                foreach (var hook in HooksFor(type, extensionManager, hooks).Committed)
                {
                    afterCommit.Add(new AfterCommitHooks.Call(hook.Name, ct => hook.Run(row, state, ct)));
                }
            }

            if (context.Database.CurrentTransaction == null)
            {
                committed = afterCommit;
            }

            var shouldReSave = false;
            foreach (var (row, type, state) in rows)
            {
                foreach (var afterSave in HooksFor(type, extensionManager, hooks).After)
                {
                    // Any hook asking for it saves again; the state passed is the one before the save
                    shouldReSave |= afterSave.Run(row, state);
                }
            }

            if (shouldReSave)
            {
                await context.SaveChangesAsync(cancellationToken);
                ClearPrefixes(cacheService, written);
            }

            if (committed == null)
            {
                // Inside the caller's transaction: they run when it commits, and never when it rolls back
                await AfterCommitHooks.RunOrDeferAsync(context as DbContext, afterCommit, cancellationToken);
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

        if (committed != null)
        {
            await AfterCommitHooks.RunOrDeferAsync(null, committed, cancellationToken);
        }

        return crudResult;
    }

    /// <summary>
    /// The named entities (with their state, whatever it is), then every other row the change tracker will write.
    /// Owned types and property bags (join tables) are part of another row and have no hooks of their own.
    /// </summary>
    private static List<(object Row, Type Type, EntityState State)> RowsToSave(IZauberDbContext context,
        IReadOnlyList<object> named)
    {
        var rows = new List<(object, Type, EntityState)>();
        var seen = new HashSet<object>(ReferenceEqualityComparer.Instance);
        var dbContext = context as DbContext;
        foreach (var item in named)
        {
            var entity = item;
            var entry = context.Entry(entity);
            if (entry.State == EntityState.Detached && dbContext != null &&
                TrackedTwin(dbContext, entry) is { } twin)
            {
                // A caller's copy whose values were put on the tracked row: the hooks get the row that is saved,
                // once
                entity = twin.Entity;
                entry = twin;
            }

            if (!seen.Add(entity)) continue;
            rows.Add((entity, entry.State == EntityState.Detached ? entity.GetType() : entry.Metadata.ClrType,
                entry.State));
        }

        if (dbContext != null)
        {
            foreach (var entry in dbContext.ChangeTracker.Entries())
            {
                if (entry.State is not (EntityState.Added or EntityState.Modified or EntityState.Deleted) ||
                    entry.Metadata.IsOwned() || entry.Metadata.HasSharedClrType || !seen.Add(entry.Entity))
                {
                    continue;
                }

                rows.Add((entry.Entity, entry.Metadata.ClrType, entry.State));
            }
        }

        return rows;
    }

    /// <summary>The tracked entry with the same type and key as a detached entity, if there is one.</summary>
    private static Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry? TrackedTwin(DbContext dbContext,
        Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry detached)
    {
        var key = detached.Metadata.FindPrimaryKey();
        if (key == null) return null;
        var values = key.Properties.Select(p => detached.Property(p.Name).CurrentValue).ToArray();
        return dbContext.ChangeTracker.Entries().FirstOrDefault(e =>
            e.Metadata == detached.Metadata &&
            key.Properties.Select(p => e.Property(p.Name).CurrentValue).SequenceEqual(values));
    }

    /// <summary>Cached reads of another type that a written type changes: a tag's links are part of the tag.</summary>
    private static readonly Dictionary<string, string> RelatedPrefixes = new()
    {
        [nameof(Tags.Models.TagItem)] = nameof(Tags.Models.Tag)
    };

    private static void ClearPrefixes(ICacheService cacheService, List<string> prefixes)
    {
        foreach (var prefix in prefixes)
        {
            if (RelatedPrefixes.TryGetValue(prefix, out var related))
            {
                cacheService.ClearCachedItemsWithPrefix(related);
            }
        }

        foreach (var prefix in prefixes)
        {
            cacheService.ClearCachedItemsWithPrefix(prefix);
        }
    }

    private sealed record SaveHook(string Name, Func<object, EntityState, bool> Run);

    private sealed record CommitHook(string Name, Func<object, EntityState, CancellationToken, Task> Run);

    private sealed record SaveHooks(IReadOnlyList<SaveHook> Before, IReadOnlyList<SaveHook> After,
        IReadOnlyList<CommitHook> Committed);

    private static readonly ConcurrentDictionary<Type, Func<ExtensionManager, SaveHooks>> HookFactories = new();

    /// <summary>The discovered hooks for one entity type, in <c>SortOrder</c>, created once per save.</summary>
    private static SaveHooks HooksFor(Type entityType, ExtensionManager extensionManager,
        Dictionary<Type, SaveHooks> created)
    {
        if (created.TryGetValue(entityType, out var hooks)) return hooks;
        var factory = HookFactories.GetOrAdd(entityType, type => typeof(DbContextExtensions)
            .GetMethod(nameof(CreateHooks), BindingFlags.NonPublic | BindingFlags.Static)!
            .MakeGenericMethod(type)
            .CreateDelegate<Func<ExtensionManager, SaveHooks>>());
        hooks = factory(extensionManager);
        created[entityType] = hooks;
        return hooks;
    }

    private static SaveHooks CreateHooks<TEntity>(ExtensionManager extensionManager) => new(
        extensionManager.GetInstances<IBeforeEntitySave<TEntity>>(true).Values.OrderBy(x => x.SortOrder)
            .Select(h => new SaveHook(h.GetType().FullName ?? h.GetType().Name,
                (row, state) => h.BeforeSave((TEntity)row, state)))
            .ToList(),
        extensionManager.GetInstances<IAfterEntitySave<TEntity>>(true).Values.OrderBy(x => x.SortOrder)
            .Select(h => new SaveHook(h.GetType().FullName ?? h.GetType().Name,
                (row, state) => h.AfterSave((TEntity)row, state)))
            .ToList(),
        extensionManager.GetInstances<IAfterEntityCommit<TEntity>>(true).Values.OrderBy(x => x.SortOrder)
            .Select(h => new CommitHook(h.GetType().FullName ?? h.GetType().Name,
                (row, state, ct) => h.AfterCommitAsync((TEntity)row, state, ct)))
            .ToList());

    /// <summary>
    /// Returns paginated list from a queryable
    /// </summary>
    /// <param name="items"></param>
    /// <param name="pageIndex"></param>
    /// <param name="pageSize"></param>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    [Obsolete("Use ToPaginatedListAsync. This blocks the thread on its queries.")]
    public static PaginatedList<T> ToPaginatedList<T>(this IQueryable<T> items, int pageIndex, int pageSize)
    {
        return new PaginatedList<T>(items, pageIndex, pageSize);
    }

    /// <summary>
    /// One page of <paramref name="items"/> (<paramref name="pageIndex"/> starts at 1), with the total count. A first
    /// page that isn't full holds every item, so no <c>COUNT</c> is sent for it. A query that isn't EF Core's (an
    /// in-memory list) is paged in memory.
    /// </summary>
    public static async Task<PaginatedList<T>> ToPaginatedListAsync<T>(this IQueryable<T> items, int pageIndex,
        int pageSize, CancellationToken cancellationToken = default)
    {
        if (items.Provider is not IAsyncQueryProvider)
        {
#pragma warning disable CS0618 // Type or member is obsolete
            return new PaginatedList<T>(items, pageIndex, pageSize);
#pragma warning restore CS0618
        }

        var skip = (pageIndex - 1) * pageSize;
        var list = await (skip > 0 ? items.Skip(skip).Take(pageSize) : items.Take(pageSize))
            .ToListAsync(cancellationToken);
        var count = skip <= 0 && list.Count < pageSize ? list.Count : await items.CountAsync(cancellationToken);
        return new PaginatedList<T>
        {
            PageIndex = pageIndex,
            Items = list,
            TotalItems = count,
            TotalPages = (int)Math.Ceiling(count / (double)pageSize)
        };
    }
}