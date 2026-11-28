using System.Linq.Dynamic.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ZauberCMS.Core.Content.Interfaces;
using ZauberCMS.Core.Content.Models;
using ZauberCMS.Core.Content.Parameters;
using ZauberCMS.Core.Data;
using ZauberCMS.Core.Extensions;
using ZauberCMS.Core.Plugins;
using ZauberCMS.Core.Settings;
using ZauberCMS.Core.Shared.Models;
using ZauberCMS.Core.Tags.Services;
using ZauberCMS.Core.Membership.Interfaces;
using ZauberCMS.Core.Membership.Models;
using ZauberCMS.Core.Shared;
using System.Text.Json;

namespace ZauberCMS.Core.Content.Services;

// Recycling, restoring and deleting pages with their subtrees, and removing orphaned blocks.
public partial class ContentService
{
    /// <summary>
    /// Deletes a content item or moves it to recycle bin. Prevents deletion when children exist. Logs audit entries.
    /// </summary>
    /// <param name="parameters">Options including content id and whether to recycle bin.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result including success state and messages.</returns>
    public async Task<HandlerResult<Models.Content>> DeleteContentAsync(DeleteContentParameters parameters,
        CancellationToken cancellationToken = default)
    {
        using var scope = serviceScopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IZauberDbContext>();
        var loggedInUser = await currentUserAccessor.GetUserAsync(cancellationToken);
        var handlerResult = new HandlerResult<Models.Content>();

        var content = await dbContext.Contents.FirstOrDefaultAsync(x => x.Id == parameters.ContentId, cancellationToken);
        if (content == null)
        {
            handlerResult.AddMessage(text["content_deleteNotFound"], ResultMessageType.Warning);
            return handlerResult;
        }

        if (parameters.MoveToRecycleBin)
        {
            // The page goes to the recycle bin with everything below it (ZA-048): a child left live would still
            // resolve by URL, be in no tree, and block the parent's permanent delete. Restore brings them back.
            // One timestamp for the page and everything binned with it, so Restore brings back exactly that group
            var recycledAt = DateTime.UtcNow;
            content.Deleted = true;
            content.DateUpdated = recycledAt;
            foreach (var descendant in await GetDescendantsAsync(dbContext, content.Id, tracked: true, cancellationToken))
            {
                if (!descendant.Deleted)
                {
                    descendant.Deleted = true;
                    descendant.DateUpdated = recycledAt;
                }
            }

            await using var recycleTransaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                AddAuditIfUser(dbContext, loggedInUser, content.Name, "Recycle Binned");
                var recycleResult = await dbContext.SaveChangesAndLog(content, handlerResult, cacheService,
                    extensionManager, cancellationToken);
                if (!recycleResult.Success)
                {
                    await recycleTransaction.RollbackAsync(cancellationToken);
                    return recycleResult;
                }

                await recycleTransaction.CommitAsync(cancellationToken);
                // Again after the commit: SaveChangesAndLog cleared before it, and a read in between cached the old rows
                cacheService.ClearCachedItemsWithPrefix(nameof(Models.Content));
                // Notify subscribers (admin tree, listings) that this content has effectively
                // been removed from the live set so they refresh — without this, recycle bin
                // moves only refresh on a manual reload.
                await appState.NotifyContentDeleted(content, loggedInUser?.UserName ?? string.Empty);
                return recycleResult;
            }
            catch
            {
                await recycleTransaction.RollbackAsync(cancellationToken);
                throw;
            }
        }

        // Permanent delete takes the items below with it only when they are all in the recycle bin (they went
        // there with this one, or on their own). A live item below is never deleted as a side effect.
        var descendants = await GetDescendantsAsync(dbContext, content.Id, tracked: true, cancellationToken);
        if (descendants.Any(x => !x.Deleted && !x.IsNestedContent))
        {
            handlerResult.AddMessage(text["content_deleteHasChildren"], ResultMessageType.Error);
            return handlerResult;
        }

        // Deepest first, then the item itself
        var toDelete = descendants.AsEnumerable().Reverse().Append(content).ToList();
        var toDeleteIds = toDelete.Select(x => x.Id).ToHashSet();

        // The blocks of every deleted item go with it, those its draft reaches too; left behind they would be
        // rows nothing points at
        var tree = new NestedContentTree(dbContext, extensionManager, logger);
        var reachedBlocks = new HashSet<Guid>();
        foreach (var stored in (await tree.LoadAsync(toDeleteIds.ToList(), cancellationToken)).Values)
        {
            reachedBlocks.UnionWith((await tree.WalkAsync(stored, null, useStoredDrafts: false, cancellationToken)).Ids);
            if (stored.UnpublishedContent != null)
            {
                reachedBlocks.UnionWith((await tree.WalkAsync(NestedContentTree.WithStoredDraft(stored), null,
                    useStoredDrafts: true, cancellationToken)).Ids);
            }
        }

        var nestedToDelete = reachedBlocks.Where(id => !toDeleteIds.Contains(id)).ToList();

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            // Remove cascaded nested rows first (their property data, then the row itself, then any UnpublishedContent)
            if (nestedToDelete.Count > 0)
            {
                var nestedPropertyValues = dbContext.ContentPropertyValues
                    .Where(pv => nestedToDelete.Contains(pv.ContentId));
                dbContext.ContentPropertyValues.RemoveRange(nestedPropertyValues);

                var nestedRows = await dbContext.Contents
                    .Where(c => nestedToDelete.Contains(c.Id))
                    .ToListAsync(cancellationToken);

                var nestedUnpublishedIds = nestedRows
                    .Where(c => c.UnpublishedContentId != null)
                    .Select(c => c.UnpublishedContentId!.Value)
                    .ToList();
                if (nestedUnpublishedIds.Count > 0)
                {
                    var nestedUnpublished = dbContext.UnpublishedContent
                        .Where(u => nestedUnpublishedIds.Contains(u.Id));
                    dbContext.UnpublishedContent.RemoveRange(nestedUnpublished);
                }

                dbContext.Contents.RemoveRange(nestedRows);

                logger.LogInformation(
                    "Hard-delete of content {ContentId} cascaded to {NestedCount} nested block content row(s)",
                    content.Id, nestedRows.Count);
            }

            foreach (var item in toDelete)
            {
                var propertyDataToDelete = dbContext.ContentPropertyValues.Where(x => x.ContentId == item.Id);
                dbContext.ContentPropertyValues.RemoveRange(propertyDataToDelete);

                if (item.UnpublishedContentId != null)
                {
                    var uContent = await dbContext.UnpublishedContent.FirstOrDefaultAsync(x => x.Id == item.UnpublishedContentId, cancellationToken);
                    if (uContent != null) dbContext.UnpublishedContent.Remove(uContent);
                }

                item.PropertyData.Clear();
            }

            if (descendants.Count > 0)
            {
                logger.LogInformation(
                    "Permanent delete of content {ContentId} also deleted {DescendantCount} recycled item(s) below it",
                    content.Id, descendants.Count);
            }

            await ContentTagLinks.StageRemoveAsync(dbContext, [.. toDeleteIds, .. nestedToDelete], cancellationToken);
            AddAuditIfUser(dbContext, loggedInUser, content.Name, "Deleted");
            foreach (var item in toDelete)
            {
                dbContext.Contents.Remove(item);
            }

            var result = await dbContext.SaveChangesAndLog(content, handlerResult, cacheService, extensionManager,
                cancellationToken);

            if (result.Success)
            {
                await transaction.CommitAsync(cancellationToken);
                // Again after the commit: SaveChangesAndLog cleared before it, and a read in between cached the old rows
                cacheService.ClearCachedItemsWithPrefix(nameof(Models.Content));
                // The deleted item, so only editors and trees showing it react (ZA-040)
                await appState.NotifyContentDeleted(content, loggedInUser?.UserName ?? string.Empty);
            }
            else
            {
                await transaction.RollbackAsync(cancellationToken);
            }

            return result;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    /// <summary>
    /// Every item below <paramref name="contentId"/> through <c>ParentId</c> (recycled ones included), nearest first.
    /// Walks the tree level by level instead of matching the stored path, so it works on every database provider
    /// and is right even where a stored path is out of date.
    /// </summary>
    private static async Task<List<Models.Content>> GetDescendantsAsync(IZauberDbContext dbContext, Guid contentId,
        bool tracked, CancellationToken cancellationToken)
    {
        var descendants = new List<Models.Content>();
        var seen = new HashSet<Guid> { contentId };
        var level = new List<Guid> { contentId };
        while (level.Count > 0)
        {
            var parentIds = level;
            var query = dbContext.Contents.Where(c => c.ParentId != null && parentIds.Contains(c.ParentId.Value));
            query = tracked ? query.AsTracking() : query.AsNoTracking();
            var children = await query.ToListAsync(cancellationToken);

            level = [];
            foreach (var child in children.Where(child => seen.Add(child.Id)))
            {
                descendants.Add(child);
                level.Add(child.Id);
            }
        }

        return descendants;
    }

    /// <summary>The ids of every item below <paramref name="contentId"/> (recycled ones included), nearest first,
    /// without loading the rows.</summary>
    private static async Task<List<Guid>> GetDescendantIdsAsync(IZauberDbContext dbContext, Guid contentId,
        CancellationToken cancellationToken)
    {
        var ids = new List<Guid>();
        var seen = new HashSet<Guid> { contentId };
        var level = new List<Guid> { contentId };
        while (level.Count > 0)
        {
            var parentIds = level;
            var children = await dbContext.Contents.AsNoTracking()
                .Where(c => c.ParentId != null && parentIds.Contains(c.ParentId.Value))
                .Select(c => c.Id)
                .ToListAsync(cancellationToken);

            level = children.Where(seen.Add).ToList();
            ids.AddRange(level);
        }

        return ids;
    }

    public async Task<HandlerResult<int>> CleanupOrphanedRelatedContentAsync(
        CleanupOrphanedRelatedContentParameters parameters, CancellationToken cancellationToken = default)
    {
        using var scope = serviceScopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IZauberDbContext>();
        var handlerResult = new HandlerResult<int>();

        try
        {
            // Find content with RelatedContentId that references non-existent content
            var orphanedContentQuery = dbContext.Contents
                .Where(c => c.RelatedContentId.HasValue &&
                            !dbContext.Contents.Any(rc => rc.Id == c.RelatedContentId.Value));

            var orphanedCount = await orphanedContentQuery.CountAsync(cancellationToken);

            if (orphanedCount > 0)
            {
                logger.LogInformation("Found {OrphanedCount} content items with orphaned RelatedContentId references",
                    orphanedCount);

                // Clear the orphaned RelatedContentId references
                await orphanedContentQuery
                    .ExecuteUpdateAsync(c => c.SetProperty(content => content.RelatedContentId, (Guid?)null),
                        cancellationToken);

                logger.LogInformation(
                    "Successfully cleared RelatedContentId for {OrphanedCount} orphaned content items", orphanedCount);

                handlerResult.Entity = orphanedCount;
                handlerResult.AddMessage(text["content_orphanedRelatedContentCleared", orphanedCount],
                    ResultMessageType.Success);
            }
            else
            {
                logger.LogInformation("No content items with orphaned RelatedContentId references found");
                handlerResult.Entity = 0;
                handlerResult.AddMessage(text["content_noOrphanedRelatedContent"], ResultMessageType.Success);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred while cleaning up orphaned RelatedContentId references");
            handlerResult.AddMessage(text["content_orphanedRelatedContentFailed", ex.Message], ResultMessageType.Error);
        }

        return handlerResult;
    }
}
