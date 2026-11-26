using System.Linq.Expressions;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ZauberCMS.Core.Content.Models;
using ZauberCMS.Core.Data;
using ZauberCMS.Core.Extensions;
using ZauberCMS.Core.Media.Interfaces;
using ZauberCMS.Core.Media.Parameters;
using ZauberCMS.Core.Media.Mapping;
using ZauberCMS.Core.Membership.Interfaces;
using ZauberCMS.Core.Membership.Models;
using ZauberCMS.Core.Plugins;
using ZauberCMS.Core.Providers;
using ZauberCMS.Core.Settings;
using ZauberCMS.Core.Audit.Interfaces;
using ZauberCMS.Core.Shared;
using ZauberCMS.Core.Shared.Models;
using ZauberCMS.Core.Shared.Services;

namespace ZauberCMS.Core.Media.Services;

public class MediaService(
    ProviderService providerService,
    IServiceScopeFactory serviceScopeFactory,
    AppState appState,
    IOptions<ZauberSettings> settings,
    ICacheService cacheService,
    ICurrentUserAccessor currentUserAccessor,
    ExtensionManager extensionManager,
    ILogger<MediaService> logger)
    : IMediaService
{
    /// <summary>
    /// Retrieves a media item with optional includes and caching.
    /// </summary>
    /// <param name="parameters">Query options including id and include flags.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Media or null.</returns>
    public async Task<Models.Media?> GetMediaAsync(GetMediaParameters parameters, CancellationToken cancellationToken = default)
    {
        using var scope = serviceScopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IZauberDbContext>();
        var query = BuildQuery(parameters, dbContext);
        if (parameters.Cached)
        {
            var cacheKey = query.GenerateCacheKey();
            return await cacheService.GetSetCachedItemAsync(cacheKey, async () => await query.FirstOrDefaultAsync(cancellationToken));
        }

        return await query.FirstOrDefaultAsync(cancellationToken);
    }

    /// <summary>
    /// Creates or updates a media item, saving or deleting the backing file via provider when needed.
    /// </summary>
    /// <param name="parameters">Media to save and storage options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result including success and messages.</returns>
    public async Task<HandlerResult<Models.Media>> SaveMediaAsync(SaveMediaParameters parameters, CancellationToken cancellationToken = default)
    {
        using var scope = serviceScopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IZauberDbContext>();

        var result = new HandlerResult<Models.Media>();
        var user = await currentUserAccessor.GetUserAsync(cancellationToken);

        // Nothing goes into a folder that is in the recycle bin (a dialog opened before the folder was binned):
        // it would be live, served, and in neither tree. Checked before a file is written.
        var parentId = parameters.ParentFolderId ?? parameters.MediaToSave?.ParentId;
        if (parentId != null &&
            await dbContext.Medias.AsNoTracking().AnyAsync(x => x.Id == parentId && x.Deleted, cancellationToken))
        {
            result.AddMessage("The folder is in the recycle bin: restore it first", ResultMessageType.Error);
            return result;
        }

        // An item in the bin is not changed (a form loaded before the delete): a change would also move its
        // DateUpdated off the stamp Restore uses to find what went to the bin with it
        if (parameters.IsUpdate && parameters.MediaToSave is { } toUpdate &&
            await dbContext.Medias.AsNoTracking().AnyAsync(x => x.Id == toUpdate.Id && x.Deleted, cancellationToken))
        {
            result.AddMessage("This media is in the recycle bin: restore it before changing it", ResultMessageType.Error);
            return result;
        }

        // If we are either creating a new file or over writing the current one
        if (parameters.FileToSave != null)
        {
            result = await providerService.StorageProvider!.SaveFile(parameters.FileToSave, parameters.MediaToSave);
            if (!result.Success)
            {
                return result;
            }
        }
        else if (parameters.MediaToSave != null)
        {
            result.Entity = parameters.MediaToSave;
        }

        if (result.Entity != null)
        {
            if (parameters.ParentFolderId != null)
            {
                result.Entity.ParentId = parameters.ParentFolderId;
            }

            result.Entity.LastUpdatedById = user?.Id;

            // Now update or add the media item
            if (parameters.IsUpdate)
            {
                // Get the DB version
                var dbMedia = dbContext.Medias
                    .FirstOrDefault(x => x.Id == result.Entity.Id);
                if (dbMedia != null)
                {
                    // Map the updated properties. In or out of the recycle bin only through Delete/Restore, which move
                    // the items below too: a form loaded before the item was binned must not bring it back on its own.
                    if (dbMedia.Deleted)
                    {
                        // Binned between the check above and here
                        result.AddMessage("This media is in the recycle bin: restore it before changing it",
                            ResultMessageType.Error);
                        return result;
                    }

                    if (result.Entity.Deleted)
                    {
                        result.AddMessage("Use Delete to move media to the recycle bin; the save left it where it is",
                            ResultMessageType.Warning);
                    }

                    result.Entity.MapTo(dbMedia);
                    dbMedia.Deleted = false;
                    result.Entity.Deleted = false;
                    dbMedia.DateUpdated = DateTime.UtcNow;

                    if (result.Entity.Url.IsNullOrWhiteSpace() && result.Entity.MediaType != Models.MediaType.Folder)
                    {
                        result.AddMessage("Url cannot be empty", ResultMessageType.Error);
                        return result;
                    }

                    // Calculate and set the Path property
                    dbMedia.Path = await result.Entity.BuildPathAsync(dbContext, parameters.IsUpdate, settings, cancellationToken);
                    var auditService = scope.ServiceProvider.GetRequiredService<IAuditService>();
                    await user.AddAudit(result.Entity, result.Entity.Name, AuditExtensions.AuditAction.Update, auditService,
                        cancellationToken);
                    result = await dbContext.SaveChangesAndLog(result.Entity, result, cacheService, extensionManager,
                        cancellationToken);
                    if (result.Success)
                    {
                        await appState.NotifyMediaSaved(dbMedia, user?.UserName ?? string.Empty);
                    }
                }
                else
                {
                    result.AddMessage("Unable to update, as no Media with that id exists", ResultMessageType.Warning);
                    return result;
                }
            }
            else
            {
                // Calculate and set the Path property
                result.Entity.Path = await result.Entity.BuildPathAsync(dbContext, parameters.IsUpdate, settings, cancellationToken);
                dbContext.Medias.Add(result.Entity);
                var auditService = scope.ServiceProvider.GetRequiredService<IAuditService>();
                await user.AddAudit(result.Entity, result.Entity.Name, AuditExtensions.AuditAction.Create, auditService,
                    cancellationToken);
                result = await dbContext.SaveChangesAndLog(result.Entity, result, cacheService, extensionManager,
                    cancellationToken);
                if (result.Success)
                {
                    await appState.NotifyMediaSaved(result.Entity, user?.UserName ?? string.Empty);
                }
            }
        }
        else
        {
            result.AddMessage("There is no media to save?", ResultMessageType.Error);
            result.Success = false;
        }

        return result;
    }

    /// <summary>
    /// Queries media items with filtering, ordering and paging. Can use cache.
    /// </summary>
    /// <param name="parameters">Query options including includes and paging.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Paged list of media.</returns>
#pragma warning disable CS1998
    public async Task<PaginatedList<Models.Media>> QueryMediaAsync(QueryMediaParameters parameters, CancellationToken cancellationToken = default)
    {
        using var scope = serviceScopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IZauberDbContext>();
        var query = BuildQuery(parameters, dbContext);
        if (parameters.Cached)
        {
            var cacheKey = $"{query.GenerateCacheKey<Models.Media>()}_Page{parameters.PageIndex}_Amount{parameters.AmountPerPage}";
            return (await cacheService.GetSetCachedItemAsync(cacheKey, async () => await query.ToPaginatedListAsync(parameters.PageIndex, parameters.AmountPerPage, cancellationToken)))!;
        }

        return await query.ToPaginatedListAsync(parameters.PageIndex, parameters.AmountPerPage, cancellationToken);
    }
#pragma warning restore CS1998

    /// <summary>
    /// Moves a media item, with everything below it, to the recycle bin (default), or deletes it for good with its
    /// files. A permanent delete takes the items below with it only when they are all in the recycle bin.
    /// </summary>
    /// <param name="parameters">Media id, recycle bin or permanent, and whether to remove the files.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result including success and messages.</returns>
    public async Task<HandlerResult<Models.Media>> DeleteMediaAsync(DeleteMediaParameters parameters, CancellationToken cancellationToken = default)
    {
        using var scope = serviceScopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IZauberDbContext>();
        var user = await currentUserAccessor.GetUserAsync(cancellationToken);
        var handlerResult = new HandlerResult<Models.Media>();

        var media = await dbContext.Medias.AsTracking().FirstOrDefaultAsync(x => x.Id == parameters.MediaId, cancellationToken);
        if (media == null)
        {
            handlerResult.AddMessage("Unable to delete, as no Media with that id exists", ResultMessageType.Warning);
            return handlerResult;
        }

        var descendants = await GetDescendantsAsync(dbContext, media.Id, cancellationToken);

        if (parameters.MoveToRecycleBin)
        {
            if (media.Deleted)
            {
                handlerResult.AddMessage("This media is already in the recycle bin", ResultMessageType.Warning);
                return handlerResult;
            }

            // The item goes to the bin with everything below it, like content (Roadmap D3): a live file left in a
            // binned folder would be in no tree. One timestamp for the group, so Restore brings back exactly that.
            var recycledAt = DateTime.UtcNow;
            media.Deleted = true;
            media.DateUpdated = recycledAt;
            foreach (var descendant in descendants.Where(x => !x.Deleted))
            {
                descendant.Deleted = true;
                descendant.DateUpdated = recycledAt;
            }

            var recycleResult = await SaveInTransactionAsync(dbContext, media, handlerResult, cancellationToken);
            if (recycleResult.Success)
            {
                await AuditAsync(scope, user, media, AuditExtensions.AuditAction.RecycleBin, cancellationToken);
                await appState.NotifyMediaDeleted(media, user?.UserName ?? string.Empty);
            }

            return recycleResult;
        }

        // A live item below is never deleted as a side effect
        if (descendants.Any(x => !x.Deleted))
        {
            handlerResult.AddMessage(
                "Unable to delete a folder that still has media in it: move that media out or to the recycle bin first",
                ResultMessageType.Error);
            return handlerResult;
        }

        var result = await PurgeAsync(dbContext, media, [media, .. descendants], parameters.DeleteFile, handlerResult,
            cancellationToken);
        if (result.Success)
        {
            await AuditAsync(scope, user, media, AuditExtensions.AuditAction.Delete, cancellationToken);
            await appState.NotifyMediaDeleted(media, user?.UserName ?? string.Empty);
        }

        return result;
    }

    /// <summary>
    /// Takes a media item out of the recycle bin with the items that went to the bin with it (same timestamp).
    /// Items binned on their own before stay in the bin. Refused while the parent folder is still in the bin.
    /// </summary>
    public async Task<HandlerResult<Models.Media>> RestoreMediaAsync(RestoreMediaParameters parameters, CancellationToken cancellationToken = default)
    {
        using var scope = serviceScopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IZauberDbContext>();
        var user = await currentUserAccessor.GetUserAsync(cancellationToken);
        var handlerResult = new HandlerResult<Models.Media>();

        var media = await dbContext.Medias.AsTracking().FirstOrDefaultAsync(x => x.Id == parameters.MediaId, cancellationToken);
        if (media == null)
        {
            handlerResult.AddMessage("Unable to restore, as no Media with that id exists", ResultMessageType.Warning);
            return handlerResult;
        }

        if (!media.Deleted)
        {
            handlerResult.AddMessage("This media is not in the recycle bin", ResultMessageType.Warning);
            return handlerResult;
        }

        if (media.ParentId is { } parentId &&
            await dbContext.Medias.AsNoTracking().AnyAsync(x => x.Id == parentId && x.Deleted, cancellationToken))
        {
            // It would be live inside a folder that is not, and in neither tree
            handlerResult.AddMessage("Restore the parent folder first: it is still in the recycle bin",
                ResultMessageType.Error);
            return handlerResult;
        }

        var binnedAt = media.DateUpdated;
        var restoredAt = DateTime.UtcNow;
        media.Deleted = false;
        media.DateUpdated = restoredAt;

        // Nearest first, so an item comes back only when its own parent is back
        var live = new HashSet<Guid> { media.Id };
        foreach (var descendant in await GetDescendantsAsync(dbContext, media.Id, cancellationToken))
        {
            if (!descendant.Deleted)
            {
                live.Add(descendant.Id);
            }
            else if (descendant.DateUpdated == binnedAt && descendant.ParentId is { } p && live.Contains(p))
            {
                descendant.Deleted = false;
                descendant.DateUpdated = restoredAt;
                live.Add(descendant.Id);
            }
        }

        var result = await SaveInTransactionAsync(dbContext, media, handlerResult, cancellationToken);
        if (result.Success)
        {
            await AuditAsync(scope, user, media, AuditExtensions.AuditAction.Restore, cancellationToken);
            await appState.NotifyMediaSaved(media, user?.UserName ?? string.Empty);
        }

        return result;
    }

    /// <summary>
    /// Permanently deletes everything in the media recycle bin, with the files. A binned folder that still holds
    /// live media (which the admin does not allow, but the data could) is kept, with the folders above it.
    /// </summary>
    public async Task<HandlerResult<int?>> EmptyMediaRecycleBinAsync(EmptyMediaRecycleBinParameters parameters, CancellationToken cancellationToken = default)
    {
        using var scope = serviceScopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IZauberDbContext>();
        var user = await currentUserAccessor.GetUserAsync(cancellationToken);
        var result = new HandlerResult<int?> { Entity = 0 };

        var binned = await dbContext.Medias.AsTracking().Where(x => x.Deleted).ToListAsync(cancellationToken);
        if (binned.Count == 0)
        {
            result.Success = true;
            return result;
        }

        var binnedById = binned.ToDictionary(x => x.Id);
        var binnedIds = binnedById.Keys.ToList();
        var keep = new HashSet<Guid>();
        var liveChildParents = await dbContext.Medias.AsNoTracking()
            .Where(x => !x.Deleted && x.ParentId != null && binnedIds.Contains(x.ParentId.Value))
            .Select(x => x.ParentId!.Value)
            .Distinct()
            .ToListAsync(cancellationToken);
        foreach (var parentId in liveChildParents)
        {
            // The folder and every binned folder above it stay, or the live item would lose its parent row
            Guid? id = parentId;
            while (id is { } current && binnedById.TryGetValue(current, out var folder) && keep.Add(current))
            {
                id = folder.ParentId;
            }
        }

        if (keep.Count > 0)
        {
            result.AddMessage(
                $"{keep.Count} folder(s) in the recycle bin still have live media in them and were kept",
                ResultMessageType.Warning);
        }

        var toDelete = binned.Where(x => !keep.Contains(x.Id)).ToList();
        if (toDelete.Count == 0)
        {
            result.Success = true;
            return result;
        }

        var purge = await PurgeAsync(dbContext, null, toDelete, parameters.DeleteFiles, new HandlerResult<Models.Media>(),
            cancellationToken);
        result.Success = purge.Success;
        result.Messages.AddRange(purge.Messages);
        if (purge.Success)
        {
            result.Entity = toDelete.Count;
            var auditService = scope.ServiceProvider.GetRequiredService<IAuditService>();
            await user.AddAudit(new Models.Media(), $"{toDelete.Count} media item(s) from the recycle bin",
                AuditExtensions.AuditAction.Delete, auditService, cancellationToken);
            await appState.NotifyMediaDeleted(null, user?.UserName ?? string.Empty);
        }

        return result;
    }

    /// <summary>
    /// Finds the content (pages and blocks) and members whose property values contain a media item's id or URL: a
    /// media picker stores the id, rich text the URL. Drafts of published pages (UnpublishedContent) are not searched.
    /// </summary>
    public async Task<List<Models.MediaUsage>> GetMediaUsageAsync(GetMediaUsageParameters parameters, CancellationToken cancellationToken = default)
    {
        if ((parameters.MediaIds.Count == 0 && !parameters.AllInRecycleBin) || parameters.Max <= 0)
        {
            return [];
        }

        using var scope = serviceScopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IZauberDbContext>();

        var ids = parameters.MediaIds.ToHashSet();
        if (parameters.AllInRecycleBin)
        {
            ids.UnionWith(await dbContext.Medias.AsNoTracking().Where(x => x.Deleted).Select(x => x.Id)
                .ToListAsync(cancellationToken));
        }

        if (parameters.IncludeDescendants)
        {
            foreach (var id in parameters.MediaIds)
            {
                ids.UnionWith((await GetDescendantsAsync(dbContext, id, cancellationToken)).Select(x => x.Id));
            }
        }

        var idList = ids.ToList();
        var media = await dbContext.Medias.AsNoTracking()
            .Where(x => idList.Contains(x.Id))
            .Select(x => new { x.Id, x.Url })
            .ToListAsync(cancellationToken);

        // What a property value would contain: the id (picker JSON, and the folder of a disk-stored file) and the URL.
        // A URL that has the id in it (every disk-stored file) is found by the id already.
        var tokens = new List<(Guid MediaId, string Token)>();
        foreach (var item in media)
        {
            var id = item.Id.ToString();
            tokens.Add((item.Id, id));
            var url = item.Url?.Trim().TrimStart('/');
            if (!url.IsNullOrWhiteSpace() && url!.Length >= 8 && !url.Contains(id, StringComparison.OrdinalIgnoreCase))
            {
                tokens.Add((item.Id, url));
            }
        }

        var usages = new List<Models.MediaUsage>();
        var seen = new HashSet<(Guid, Models.MediaUsageSource, Guid)>();
        foreach (var chunk in tokens.Chunk(UsageTokensPerQuery))
        {
            // Always the same number of terms (the last batch repeats one), so the query is compiled once
            var chunkTokens = chunk.Select(x => x.Token).ToList();
            while (chunkTokens.Count < UsageTokensPerQuery)
            {
                chunkTokens.Add(chunkTokens[0]);
            }

            var contentRows = await dbContext.ContentPropertyValues.AsNoTracking()
                .Where(ContainsAny<ContentPropertyValue>(v => v.Value, chunkTokens))
                .Select(v => new { v.ContentId, v.Value, v.Content!.Name, v.Content.Deleted })
                .ToListAsync(cancellationToken);
            foreach (var row in contentRows)
            {
                foreach (var (mediaId, token) in chunk.Where(t => row.Value.Contains(t.Token, StringComparison.Ordinal)))
                {
                    if (seen.Add((mediaId, Models.MediaUsageSource.Content, row.ContentId)))
                    {
                        usages.Add(new Models.MediaUsage(mediaId, Models.MediaUsageSource.Content, row.ContentId, row.Name, row.Deleted));
                    }
                }
            }

            var userRows = await dbContext.UserPropertyValues.AsNoTracking()
                .Where(ContainsAny<UserPropertyValue>(v => v.Value, chunkTokens))
                .Select(v => new { v.UserId, v.Value, v.User!.UserName, v.User.Deleted })
                .ToListAsync(cancellationToken);
            foreach (var row in userRows)
            {
                foreach (var (mediaId, token) in chunk.Where(t => row.Value.Contains(t.Token, StringComparison.Ordinal)))
                {
                    if (seen.Add((mediaId, Models.MediaUsageSource.User, row.UserId)))
                    {
                        usages.Add(new Models.MediaUsage(mediaId, Models.MediaUsageSource.User, row.UserId, row.UserName, row.Deleted));
                    }
                }
            }

            if (usages.Count >= parameters.Max)
            {
                break;
            }
        }

        return usages.Take(parameters.Max).ToList();
    }

    private const int UsageTokensPerQuery = 100;

    /// <summary>
    /// <c>x => x.Value.Contains(t1) || x.Value.Contains(t2) || …</c>, translated by every provider. Each term is
    /// read from a holder object, as a captured variable would be, so EF sends it as a parameter.
    /// </summary>
    private static Expression<Func<T, bool>> ContainsAny<T>(Expression<Func<T, string>> property, IReadOnlyList<string> tokens)
    {
        var contains = typeof(string).GetMethod(nameof(string.Contains), [typeof(string)])!;
        Expression? body = null;
        foreach (var token in tokens)
        {
            var value = Expression.Property(Expression.Constant(new UsageToken(token)), nameof(UsageToken.Value));
            Expression call = Expression.Call(property.Body, contains, value);
            body = body == null ? call : Expression.OrElse(body, call);
        }

        return Expression.Lambda<Func<T, bool>>(body ?? Expression.Constant(false), property.Parameters);
    }

    private sealed record UsageToken(string Value);

    /// <summary>Everything below a media item, nearest first, tracked.</summary>
    private static async Task<List<Models.Media>> GetDescendantsAsync(IZauberDbContext dbContext, Guid mediaId,
        CancellationToken cancellationToken)
    {
        var descendants = new List<Models.Media>();
        var seen = new HashSet<Guid> { mediaId };
        var level = new List<Guid> { mediaId };
        while (level.Count > 0)
        {
            var parentIds = level;
            var children = await dbContext.Medias.AsTracking()
                .Where(x => x.ParentId != null && parentIds.Contains(x.ParentId.Value))
                .ToListAsync(cancellationToken);

            level = [];
            foreach (var child in children.Where(child => seen.Add(child.Id)))
            {
                descendants.Add(child);
                level.Add(child.Id);
            }
        }

        return descendants;
    }

    private async Task<HandlerResult<Models.Media>> SaveInTransactionAsync(IZauberDbContext dbContext, Models.Media? media,
        HandlerResult<Models.Media> handlerResult, CancellationToken cancellationToken,
        Func<HandlerResult<Models.Media>, Task<bool>>? beforeCommit = null)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var result = await dbContext.SaveChangesAndLog(media, handlerResult, cacheService, extensionManager, cancellationToken);
            if (!result.Success || (beforeCommit != null && !await beforeCommit(result)))
            {
                result.Success = false;
                await transaction.RollbackAsync(CancellationToken.None);
                // SaveChangesAndLog cleared the cache for rows that are now back
                cacheService.ClearCachedItemsWithPrefix(nameof(Models.Media));
                return result;
            }

            await transaction.CommitAsync(cancellationToken);
            // Again after the commit: SaveChangesAndLog cleared before it, and a read in between cached the old rows
            cacheService.ClearCachedItemsWithPrefix(nameof(Models.Media));
            return result;
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    /// <summary>
    /// Deletes the rows and the files together: the files go after the rows are written and before the commit, and
    /// a file that can't be removed rolls the rows back. A file left on disk without its row would be served to
    /// anyone (the recycle bin and the restriction only know files through their rows). A file that a media item
    /// that stays still points at is kept.
    /// </summary>
    private async Task<HandlerResult<Models.Media>> PurgeAsync(IZauberDbContext dbContext, Models.Media? media,
        List<Models.Media> items, bool deleteFiles, HandlerResult<Models.Media> handlerResult, CancellationToken cancellationToken)
    {
        var storage = providerService.StorageProvider;
        if (deleteFiles && storage == null)
        {
            handlerResult.AddMessage("No storage provider is set up, so the files can't be removed", ResultMessageType.Error);
            return handlerResult;
        }

        var ids = items.Select(x => x.Id).ToList();
        var urls = items.Select(x => x.Url).Where(x => !x.IsNullOrWhiteSpace()).Select(x => x!).Distinct().ToList();

        // Stored URLs may differ in a leading slash or the slash direction: look for each form, compare normalised
        var urlForms = urls
            .SelectMany(u =>
            {
                var core = u.Replace('\\', '/').TrimStart('/');
                return new[] { u, core, "/" + core };
            })
            .Distinct()
            .ToList();
        var keptUrls = urls.Count == 0
            ? []
            : (await dbContext.Medias.AsNoTracking()
                .Where(x => !ids.Contains(x.Id) && x.Url != null && urlForms.Contains(x.Url))
                .Select(x => x.Url!)
                .ToListAsync(cancellationToken))
            .Select(NormaliseMediaPath)
            .ToHashSet();
        var filesToDelete = deleteFiles
            ? urls.Where(u => !keptUrls.Contains(NormaliseMediaPath(u))).ToList()
            : [];

        dbContext.Medias.RemoveRange(items);
        return await SaveInTransactionAsync(dbContext, media, handlerResult, cancellationToken, async result =>
        {
            foreach (var url in filesToDelete)
            {
                try
                {
                    await storage!.DeleteFile(url);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Media file {Url} could not be removed from storage", url);
                    result.AddMessage(
                        $"The file '{url}' could not be removed from storage, so nothing was deleted. Try again; files " +
                        "removed before it are already gone.", ResultMessageType.Error);
                    return false;
                }
            }

            return true;
        });
    }

    private static async Task AuditAsync(IServiceScope scope, User? user, Models.Media media,
        AuditExtensions.AuditAction action, CancellationToken cancellationToken)
    {
        var auditService = scope.ServiceProvider.GetRequiredService<IAuditService>();
        await user.AddAudit(media, media.Name, action, auditService, cancellationToken);
    }

    /// <summary>
    /// Checks if a media item has child media.
    /// </summary>
    /// <param name="parameters">Parent id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True when children exist.</returns>
    public async Task<bool> HasChildMediaAsync(HasChildMediaParameters parameters, CancellationToken cancellationToken = default)
    {
        using var scope = serviceScopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IZauberDbContext>();
        return await dbContext.Medias.AsNoTracking().AnyAsync(c => c.ParentId == parameters.ParentId && !c.Deleted, cancellationToken: cancellationToken);
    }

    /// <inheritdoc />
    public async Task<TreeLevel<Models.Media>> GetMediaTreeLevelAsync(MediaTreeLevelParameters parameters,
        CancellationToken cancellationToken = default)
    {
        using var scope = serviceScopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IZauberDbContext>();

        // The same types in a folder's children and in its "has children" flag: a folder holding only filtered-out
        // files gets no expand arrow
        var types = parameters.MediaTypes.Count == 0
            ? null
            : parameters.MediaTypes.Append(Models.MediaType.Folder).Distinct().ToList();
        var live = dbContext.Medias.AsNoTracking().Where(m => !m.Deleted);
        if (types != null)
        {
            live = live.Where(m => types.Contains(m.MediaType));
        }

        IQueryable<Models.Media> query;
        if (parameters.Ids != null)
        {
            var ids = parameters.Ids;
            query = live.Where(m => ids.Contains(m.Id));
        }
        else
        {
            query = live.Where(m => m.ParentId == parameters.ParentId);
        }

        var items = await query
            .OrderBy(m => m.Name).ThenBy(m => m.Id)
            .Skip(parameters.Skip).Take(parameters.Take)
            .ToListAsync(cancellationToken);
        var total = parameters.Skip == 0 && items.Count < parameters.Take
            ? items.Count
            : await query.CountAsync(cancellationToken);

        var folderIds = items.Where(m => m.MediaType == Models.MediaType.Folder).Select(m => m.Id).ToList();
        var withChildren = folderIds.Count == 0
            ? []
            : await live
                .Where(m => m.ParentId != null && folderIds.Contains(m.ParentId.Value))
                .Select(m => m.ParentId!.Value)
                .Distinct()
                .ToListAsync(cancellationToken);

        return new TreeLevel<Models.Media>
        {
            Items = items,
            IdsWithChildren = withChildren.ToHashSet(),
            TotalItems = total,
            Skip = parameters.Skip
        };
    }

    /// <summary>
    /// Returns a dictionary of restricted media URLs to ids. Uses cache.
    /// </summary>
    /// <param name="parameters">Caching flag.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Dictionary URL -> MediaId.</returns>
    public async Task<Dictionary<string, Guid>> GetRestrictedMediaUrlsAsync(GetRestrictedMediaUrlsParameters parameters, CancellationToken cancellationToken = default)
    {
        using var scope = serviceScopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IZauberDbContext>();
        var query = BuildQuery(dbContext);
        if (parameters.Cached)
        {
            var cacheKey = query.GenerateCacheKey<Models.Media>();
            return await cacheService.GetSetCachedItemAsync(cacheKey, async () => await query.Select(x => new { x.Url, x.Id }).ToDictionaryAsync(x => x.Url ?? string.Empty, x => x.Id, cancellationToken: cancellationToken)) ?? new Dictionary<string, Guid>();
        }

        return await query.Select(x => new { x.Url, x.Id }).ToDictionaryAsync(x => x.Url ?? string.Empty, x => x.Id, cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Returns a dictionary of restricted media URLs to access descriptors (id + allowed role names).
    /// An entry whose <see cref="Models.RestrictedMediaEntry.AllowedRoleNames"/> is empty grants
    /// access to any authenticated user; otherwise the user must be in at least one named role.
    /// Files whose media is in the recycle bin are listed too, with <see cref="Models.RestrictedMediaEntry.InRecycleBin"/>.
    /// </summary>
    public async Task<Dictionary<string, Models.RestrictedMediaEntry>> GetRestrictedMediaAccessAsync(GetRestrictedMediaUrlsParameters parameters, CancellationToken cancellationToken = default)
    {
        using var scope = serviceScopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IZauberDbContext>();
        // Identifier-based key keeps the "Media-" prefix so SaveChangesAndLog's
        // ClearCachedItemsWithPrefix(typeof(Media).Name) invalidates this entry on save.
        var cacheKey = "RestrictedMediaAccess".GenerateCacheKey<Models.Media>();

        async Task<Dictionary<string, Models.RestrictedMediaEntry>> Execute()
        {
            var rows = await dbContext.Medias.AsNoTracking()
                .Where(x => x.Url != null && (x.RequiresAuthentication || x.Deleted))
                .Select(x => new
                {
                    x.Url,
                    x.Id,
                    x.Deleted,
                    RoleNames = x.MediaRoles.Select(mr => mr.Role.Name ?? string.Empty).ToList()
                })
                .ToListAsync(cancellationToken);

            // A recycled item's file is still served to the public when a live, unrestricted item has the same file
            var binnedUrls = rows.Where(x => x.Deleted).Select(x => x.Url!).Distinct().ToList();
            var publicUrls = binnedUrls.Count == 0
                ? []
                : (await dbContext.Medias.AsNoTracking()
                    .Where(x => !x.Deleted && !x.RequiresAuthentication && x.Url != null && binnedUrls.Contains(x.Url))
                    .Select(x => x.Url!)
                    .ToListAsync(cancellationToken))
                .Select(NormaliseMediaPath)
                .ToHashSet();

            // Keys are normalised (see NormaliseMediaPath) so the lookup does not depend on the case or leading
            // slash of the request, and survives a distributed cache (which would drop a custom comparer).
            // Two rows with the same file: the roles are combined, never dropped. While any item with the file is
            // live, the live items decide; when all of them are in the recycle bin, only administrators get it.
            var entries = new Dictionary<string, Models.RestrictedMediaEntry>();
            foreach (var group in rows.GroupBy(x => NormaliseMediaPath(x.Url)))
            {
                var live = group.Where(x => !x.Deleted).ToList();
                if (live.Count > 0)
                {
                    entries[group.Key] = new Models.RestrictedMediaEntry(live[0].Id,
                        live.SelectMany(x => x.RoleNames).Distinct().ToList());
                }
                else if (!publicUrls.Contains(group.Key))
                {
                    entries[group.Key] = new Models.RestrictedMediaEntry(group.First().Id, [], InRecycleBin: true);
                }
            }

            return entries;
        }

        if (parameters.Cached)
        {
            return await cacheService.GetSetCachedItemAsync(cacheKey, Execute) ?? new Dictionary<string, Models.RestrictedMediaEntry>();
        }

        return await Execute();
    }

    private static IQueryable<Models.Media> BuildQuery(GetMediaParameters parameters, IZauberDbContext dbContext)
    {
        var query = dbContext.Medias.AsQueryable();

        if (parameters.AsNoTracking)
        {
            query = query.AsNoTracking();
        }

        if (!parameters.IncludeDeleted)
        {
            query = query.Where(x => !x.Deleted);
        }

        if (parameters.IncludeParent)
        {
            query = query.Include(x => x.Parent);
        }

        if (parameters.IncludeChildren)
        {
            // Never the binned ones, whatever IncludeDeleted asks of the item itself (as for content, ZA-065):
            // callers list, sort and save the children
            query = query.Include(x => x.Children.Where(c => !c.Deleted));

            if (parameters.IncludeParent)
            {
                query = query.AsSplitQuery();
            }
        }

        if (parameters.MediaType != null)
        {
            query = query.Where(x => x.MediaType == parameters.MediaType);
        }

        if (parameters.Id != null)
        {
            query = query.Where(x => x.Id == parameters.Id);
        }

        return query;
    }

    private static IQueryable<Models.Media> BuildQuery(QueryMediaParameters parameters, IZauberDbContext dbContext)
    {
        var query = dbContext.Medias.Include(x => x.Parent).AsQueryable();

        if (parameters.Query != null)
        {
            query = parameters.Query.Invoke();
        }
        else
        {
            if (!parameters.IncludeDeleted)
            {
                query = query.Where(x => !x.Deleted);
            }

            if (parameters.IncludeChildren)
            {
                query = query.Include(x => x.Children.Where(c => !c.Deleted)).AsSplitQuery();
            }

            if (parameters.AsNoTracking)
            {
                query = query.AsNoTracking();
            }

            if (parameters.Ids.Count != 0)
            {
                query = query.Where(x => parameters.Ids.Contains(x.Id));
                parameters.AmountPerPage = parameters.Ids.Count;
            }

            if (parameters.MediaTypes.Count != 0)
            {
                query = query.Where(x => parameters.MediaTypes.Contains(x.MediaType));
            }
        }

        if (parameters.WhereClause != null)
        {
            query = query.Where(parameters.WhereClause);
        }

        query = parameters.OrderBy switch
        {
            GetMediaOrderBy.DateUpdated => query.OrderBy(p => p.DateUpdated),
            GetMediaOrderBy.DateUpdatedDescending => query.OrderByDescending(p => p.DateUpdated),
            GetMediaOrderBy.DateCreated => query.OrderBy(p => p.DateCreated),
            GetMediaOrderBy.DateCreatedDescending => query.OrderByDescending(p => p.DateCreated),
            GetMediaOrderBy.Name => query.OrderBy(p => p.Name),
            GetMediaOrderBy.NameDescending => query.OrderByDescending(p => p.Name),
            _ => query.OrderByDescending(p => p.DateUpdated)
        };

        return query;
    }

    private static IQueryable<Models.Media> BuildQuery(IZauberDbContext dbContext)
    {
        // Recycled media is included on purpose: the file is still on disk, so it must stay protected (ZA-053).
        return dbContext.Medias.AsNoTracking().Where(x => x.RequiresAuthentication);
    }

    /// <summary>
    /// The form restricted media paths are compared in: forward slashes, no leading slash, no doubled slashes,
    /// lower case. File systems on Windows and macOS ignore case, so "/Media/A.PDF" and "/media/a.pdf" are the same
    /// file, and the static file handler reads "/media//a.pdf" as "/media/a.pdf" (the server does not merge "//").
    /// </summary>
    internal static string NormaliseMediaPath(string? path)
    {
        var normalised = (path ?? string.Empty).Replace('\\', '/');
        while (normalised.Contains("//", StringComparison.Ordinal))
        {
            normalised = normalised.Replace("//", "/", StringComparison.Ordinal);
        }

        return normalised.TrimStart('/').ToLowerInvariant();
    }
}
