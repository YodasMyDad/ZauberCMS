using System.Linq.Dynamic.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ZauberCMS.Core.Content.Interfaces;
using ZauberCMS.Core.Content.Models;
using ZauberCMS.Core.Content.Parameters;
using ZauberCMS.Core.Content.Mapping;
using ZauberCMS.Core.Data;
using ZauberCMS.Core.Extensions;
using ZauberCMS.Core.Languages.Interfaces;
using ZauberCMS.Core.Plugins;
using ZauberCMS.Core.Settings;
using ZauberCMS.Core.Shared.Models;
using ZauberCMS.Core.Tags.Services;
using ZauberCMS.Core.Shared.Services;
using ZauberCMS.Core.Membership.Interfaces;
using ZauberCMS.Core.Membership.Models;
using ZauberCMS.Core.Shared;
using System.Text.Json;

namespace ZauberCMS.Core.Content.Services;

public partial class ContentService(
    IServiceScopeFactory serviceScopeFactory,
    ICacheService cacheService,
    IOptions<ZauberSettings> settings,
    ICurrentUserAccessor currentUserAccessor,
    ExtensionManager extensionManager,
    AppState appState,
    ILanguageService languageService,
    ILogger<ContentService> logger)
    : IContentService
{
    private const string BlockListEditorComponentAlias = BlockListNestedContentReferenceProvider.BlockListEditorAlias;

    /// <summary>
    /// Retrieves a single content item based on the provided parameters. Can optionally use cache.
    /// </summary>
    /// <param name="parameters">Query options such as id, type, includes and caching.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The matching content item or null.</returns>
    public async Task<Models.Content?> GetContentAsync(GetContentParameters parameters,
        CancellationToken cancellationToken = default)
    {
        using var scope = serviceScopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IZauberDbContext>();
        var query = BuildQuery(parameters, dbContext);
        if (parameters.Cached)
        {
            // The key renders the SQL: only worth it when the result is cached (ZA-062)
            var cacheKey = query.GenerateCacheKey();
            return await cacheService.GetSetCachedItemAsync(cacheKey,
                async () => await FetchContentAsync(query, cancellationToken));
        }

        return await FetchContentAsync(query, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<HandlerResult<Models.Content>> SaveContentAsync(SaveContentParameters parameters,
        CancellationToken cancellationToken = default)
    {
        if (parameters.Content != null && !parameters.ExcludePropertyData)
        {
            // A save of the item's values is a save of the item with its blocks (ContentService.Tree.cs)
            return await SaveWithBlocksAsync(parameters, cancellationToken);
        }

        // Sort, move, restore, restrict access, culture: where or how the item is shown, not its values or blocks
        using var scope = serviceScopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IZauberDbContext>();
        var user = await currentUserAccessor.GetUserAsync(cancellationToken);
        var handlerResult = new HandlerResult<Models.Content>();

        if (parameters.Content == null)
        {
            handlerResult.AddMessage("Content is null", ResultMessageType.Error);
            return handlerResult;
        }

        if (parameters.SaveUnpublishedOnly)
        {
            await StageDraftAsync(dbContext, parameters.Content, parameters.Content.UnpublishedContentId,
                cancellationToken);

            var unpublishedResult = await dbContext.SaveChangesAndLog(null, handlerResult, cacheService,
                extensionManager, cancellationToken);

            // Create version for unpublished content too
            if (unpublishedResult.Success)
            {
                try
                {
                    var versioningService = scope.ServiceProvider.GetService<IContentVersioningService>();
                    if (versioningService != null)
                    {
                        // Use the parameter content which has the updated PropertyData from the UI
                        var versionParameters = new CreateContentVersionParameters
                        {
                            Content = parameters.Content,
                            Status = ContentVersionStatus.Draft,
                            IsAutoSave = false,
                            Comments = "Draft saved - unpublished changes",
                            CreatedByUserId = user?.Id
                        };

                        await versioningService.CreateVersionAsync(versionParameters, cancellationToken);
                    }
                    else
                    {
                        logger.LogWarning("ContentVersioningService is null - versioning will not work for unpublished content");
                    }
                }
                catch (Exception ex)
                {
                    // Log version creation error but don't fail the content save
                    logger.LogError(ex, "Failed to create draft version for unpublished content {ContentId}: {ErrorMessage}", 
                        parameters.Content.Id, ex.Message);
                }
            }

            return unpublishedResult;
        }

        var staged = await StageContentAsync(dbContext, user, parameters, handlerResult, validateBlockTypes: true,
            cancellationToken);
        if (staged == null)
        {
            return handlerResult;
        }

        var (content, isUpdate) = staged;

        // The audit row is written by the same save, so a failed or vetoed save leaves none. The transaction holds
        // a second save an after-save hook asks for.
        HandlerResult<Models.Content> saveResult;
        await using (var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken))
        {
            try
            {
                var nameText = content.Name ?? nameof(Models.Content);
                var actionText = isUpdate ? "Updated" : "Created";
                AddAuditIfUser(dbContext, user, nameText, actionText);

                saveResult = await dbContext.SaveChangesAndLog(content, handlerResult, cacheService, extensionManager,
                    cancellationToken);

                if (saveResult.Success)
                {
                    await transaction.CommitAsync(cancellationToken);
                    // Again after the commit: SaveChangesAndLog cleared before it, and a read in between cached the old rows
                    cacheService.ClearCachedItemsWithPrefix(nameof(Models.Content));
                }
                else
                {
                    await transaction.RollbackAsync(cancellationToken);
                }
            }
            catch (DbUpdateConcurrencyException)
            {
                await transaction.RollbackAsync(cancellationToken);
                handlerResult.AddMessage(
                    "Content was modified by another user — please reload and re-apply your changes",
                    ResultMessageType.Error);
                return handlerResult;
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        }

        // This path changes no values, so the blocks stay as they are; the version below snapshots them as stored.

        // Create version after successful save
        if (saveResult is { Success: true, Entity: not null })
        {
            await CreateVersionAfterSaveAsync(scope, parameters.Content, content.Published
                    ? ContentVersionStatus.Published
                    : ContentVersionStatus.Draft,
                content.Published
                    ? (isUpdate ? "Content updated and published" : "Content created and published")
                    : (isUpdate ? "Content updated" : "Content created"),
                user?.Id, content.Id, cancellationToken);
        }

        return saveResult;
    }

    /// <summary>What <see cref="StageContentAsync"/> staged: the tracked entity and whether it already existed.</summary>
    private sealed record StagedContent(Models.Content Content, bool IsUpdate);

    /// <summary>
    /// Stages a draft ("Save only" of published content): the item's values go into its stored draft, the live row
    /// only gets the link to it. Nothing is saved yet.
    /// </summary>
    /// <param name="storedDraftId">The id of the item's draft, if it has one.</param>
    private static async Task StageDraftAsync(IZauberDbContext dbContext, Models.Content source, Guid? storedDraftId,
        CancellationToken cancellationToken)
    {
        var draft = storedDraftId == null
            ? null
            : await dbContext.UnpublishedContent.FirstOrDefaultAsync(x => x.Id == storedDraftId, cancellationToken);
        var isNew = draft == null;
        draft ??= new UnpublishedContent();
        source.MapTo(draft.JsonContent);
        draft.JsonContent.PropertyData = source.PropertyData;
        draft.JsonContent.UnpublishedContentId = draft.Id;
        draft.DateUpdated = DateTime.UtcNow;

        if (isNew)
        {
            var row = await dbContext.Contents.FirstOrDefaultAsync(x => x.Id == source.Id, cancellationToken);
            if (row != null)
            {
                row.UnpublishedContentId = draft.Id;
            }

            dbContext.UnpublishedContent.Add(draft);
        }
    }

    /// <summary>Creates the content's version after a save. A failure is logged and does not fail the save.</summary>
    private async Task CreateVersionAfterSaveAsync(IServiceScope scope, Models.Content content,
        ContentVersionStatus status, string comments, Guid? userId, Guid contentId,
        CancellationToken cancellationToken)
    {
        try
        {
            var versioningService = scope.ServiceProvider.GetService<IContentVersioningService>();
            if (versioningService == null)
            {
                logger.LogWarning("ContentVersioningService is null - versioning will not work");
                return;
            }

            await versioningService.CreateVersionAsync(new CreateContentVersionParameters
            {
                Content = content,
                Status = status,
                IsAutoSave = false,
                Comments = comments,
                CreatedByUserId = userId
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            // Log version creation error but don't fail the content save
            logger.LogError(ex, "Failed to create content version for content {ContentId}: {ErrorMessage}",
                contentId, ex.Message);
        }
    }

    /// <summary>
    /// Stages a create or update of one content item on <paramref name="dbContext"/>, with every check
    /// <see cref="SaveContentAsync"/> makes. Nothing is saved yet. Returns null when a check failed (the reason is in
    /// <paramref name="handlerResult"/>).
    /// </summary>
    private async Task<StagedContent?> StageContentAsync(IZauberDbContext dbContext, User? user,
        SaveContentParameters parameters, HandlerResult<Models.Content> handlerResult, bool validateBlockTypes,
        CancellationToken cancellationToken)
    {
        if (parameters.Content == null)
        {
            handlerResult.AddMessage("Content is null", ResultMessageType.Error);
            return null;
        }

        var isUpdate = true;
        var unpublishedContent =
            await dbContext.UnpublishedContent.FirstOrDefaultAsync(x => x.Id == parameters.Content.UnpublishedContentId, cancellationToken);

        var urlGenerated = false;
        if (parameters.Content.IsNestedContent)
        {
            // Blocks are not pages: no URL, so they never resolve on the front end and never take a page's
            // slug (ZA-067). Saving a block clears a URL an older version gave it.
#pragma warning disable CS0618 // Type or member is obsolete
            parameters.Content.Url = null;
#pragma warning restore CS0618 // Type or member is obsolete
        }
        else if (parameters.Content.Url().IsNullOrWhiteSpace())
        {
            var baseSlug = new SlugHelper().GenerateSlug(parameters.Content.Name);
#pragma warning disable CS0618 // Type or member is obsolete
            parameters.Content.Url = await GenerateUniqueUrlAsync(dbContext, baseSlug, cancellationToken, parameters.Content.Id);
#pragma warning restore CS0618 // Type or member is obsolete
            urlGenerated = true;
        }
        else
        {
            // The front-end lookup normalises the request slug the same way (ContentUrl.Normalize), so a
            // stored URL with mixed case or slashes around it would never match (ZA-066).
#pragma warning disable CS0618 // Type or member is obsolete
            parameters.Content.Url = ContentUrl.Normalize(parameters.Content.Url);
#pragma warning restore CS0618 // Type or member is obsolete
        }

        if (parameters.Content.ContentTypeAlias.IsNullOrWhiteSpace())
        {
            var contentType = dbContext.ContentTypes.AsTracking()
                .FirstOrDefault(x => x.Id == parameters.Content.ContentTypeId);
            parameters.Content.ContentTypeAlias = contentType?.Alias;
        }

        var content = dbContext.Contents
            .Include(x => x.PropertyData)
            .Include(x => x.ContentRoles).ThenInclude(x => x.Role)
            .FirstOrDefault(x => x.Id == parameters.Content.Id);

        // The stored state before this save, to see what the save changes
        var wasDeleted = content?.Deleted ?? false;
        var storedDateUpdated = content?.DateUpdated;
        var storedParentId = content?.ParentId;
        var storedPath = content?.Path.ToList() ?? [];
        var storedUrl = content?.Url();

        if (content == null)
        {
            isUpdate = false;
            content = parameters.Content;

            // Auto-assign sort order for new content items (only for regular content, not element types/block lists)
            if (content.SortOrder == 0 && !content.IsNestedContent)
            {
                content.SortOrder = await GetNextSortOrderAsync(dbContext, content.ParentId, content.IsRootContent, cancellationToken) + 1;
            }

            content.LastUpdatedById = user?.Id;
            dbContext.Contents.Add(content);
        }
        else
        {
            // Capture the user's submitted DateUpdated BEFORE MapTo overwrites the tracked value.
            // This becomes the concurrency-token original — i.e. "the version the user was looking at
            // when they hit save". If the DB row's DateUpdated differs, the UPDATE's WHERE clause
            // won't match and EF throws DbUpdateConcurrencyException. Without this, the load above
            // would just snapshot the latest DB value as the "original", silently accepting stale saves.
            var submittedDateUpdated = parameters.Content.DateUpdated;
            var storedUnpublishedContentId = content.UnpublishedContentId;

            parameters.Content.MapTo(content);
            if (parameters.ExcludePropertyData)
            {
                // A save without property data keeps the draft, so it keeps the link to it too: the caller's copy
                // may be older than the draft (a tree node loaded before "Save only"), and creating a draft does
                // not change DateUpdated, so the concurrency check would not catch it (ZA-047)
                content.UnpublishedContentId = storedUnpublishedContentId;
            }

            content.LastUpdatedById = user?.Id;
            content.DateUpdated = DateTime.UtcNow;

            if (submittedDateUpdated != default)
            {
                // String-based Property() because IZauberDbContext exposes the non-generic Entry(object)
                // overload only — the strongly-typed expression overload requires EntityEntry<T>.
                dbContext.Entry(content).Property(nameof(Models.Content.DateUpdated)).OriginalValue = submittedDateUpdated;
            }

            if (!parameters.ExcludePropertyData)
            {
                UpdateContentPropertyValues(dbContext, content, parameters.Content.PropertyData);
            }
        }

        if (parameters.UpdateContentRoles)
        {
            await UpdateContentRolesAsync(dbContext, content, parameters, cancellationToken);
        }

        // A save with property data publishes the editor's values, so a pending draft is replaced by it. A save
        // without property data (sort, move, restrict access, culture & domains, restore) only changes where or
        // how the page is shown, so its draft stays (ZA-047).
        if (unpublishedContent != null && !parameters.ExcludePropertyData)
        {
            dbContext.UnpublishedContent.Remove(unpublishedContent);
        }

        // The items below, loaded once (tracked) for the loop check and the path rewrite (ZA-145)
        List<Models.Content>? descendants = null;
        if (isUpdate && content.ParentId != storedParentId && content.ParentId != null)
        {
            // A page can't be moved under itself or one of its own descendants: the tree would become a loop
            var newParentId = content.ParentId.Value;
            descendants = await GetDescendantsAsync(dbContext, content.Id, tracked: true, cancellationToken);
            var isOwnDescendant = newParentId == content.Id || descendants.Any(x => x.Id == newParentId);
            if (isOwnDescendant)
            {
                handlerResult.AddMessage("Content can't be moved below itself", ResultMessageType.Error);
                return null;
            }
        }

        content.Path = await content.BuildPathAsync(dbContext, isUpdate, settings, cancellationToken);

        var isRestore = isUpdate && wasDeleted && !content.Deleted;
        if (isRestore && content.ParentId is { } restoreParentId &&
            await dbContext.Contents.AsNoTracking().AnyAsync(x => x.Id == restoreParentId && x.Deleted, cancellationToken))
        {
            // It would be live under a page that is not, unreachable in both trees (ZA-048)
            handlerResult.AddMessage("Restore the parent page first: it is still in the recycle bin",
                ResultMessageType.Error);
            return null;
        }

        var pathChanged = isUpdate && !storedPath.SequenceEqual(content.Path);
        var reservedUrls = new HashSet<string>(StringComparer.Ordinal);

        // Every page needs its own URL: the front end resolves a URL to one page, and the language lookup keys on
        // it (ZA-046). A URL typed by the editor is refused when taken; a generated or restored one gets a suffix.
        if (!content.IsNestedContent && !content.Deleted && !content.Url().IsNullOrWhiteSpace())
        {
            var url = content.Url()!;
            var urlChanged = !isUpdate || isRestore || !string.Equals(url, storedUrl, StringComparison.Ordinal);
            if (urlChanged && await IsUrlTakenAsync(dbContext, url, content.Id, cancellationToken))
            {
                if (urlGenerated || isRestore)
                {
#pragma warning disable CS0618 // Type or member is obsolete
                    content.Url = await GenerateUniqueUrlAsync(dbContext, url, cancellationToken, content.Id);
#pragma warning restore CS0618 // Type or member is obsolete
                    if (isRestore)
                    {
                        await KeepDraftUrlAsync(dbContext, content, cancellationToken);
                        handlerResult.AddMessage(
                            $"The URL '{url}' is now used by another page, so the restored page has the URL '{content.Url()}'",
                            ResultMessageType.Warning);
                    }
                }
                else
                {
                    handlerResult.AddMessage($"The URL '{url}' is already used by another page", ResultMessageType.Error);
                    return null;
                }
            }

            reservedUrls.Add(content.Url()!);
        }

        if (isRestore || pathChanged)
        {
            // Restore brings back the pages that went to the recycle bin with this one (ZA-048), and a move or
            // re-parent rewrites the stored path of everything below it (ZA-079). Nearest first, so a parent's
            // new path is known before its children are updated.
            var newPaths = new Dictionary<Guid, List<Guid>> { [content.Id] = content.Path };
            descendants ??= await GetDescendantsAsync(dbContext, content.Id, tracked: true, cancellationToken);
            foreach (var descendant in descendants)
            {
                if (descendant.ParentId is { } parentId && newPaths.TryGetValue(parentId, out var parentPath))
                {
                    descendant.Path = [.. parentPath, descendant.Id];
                }

                newPaths[descendant.Id] = descendant.Path;

                // Only the pages that went to the bin with this one: recycling stamps them with the same DateUpdated.
                // A page binned on its own earlier stays in the bin (and shows there once this one is back).
                if (isRestore && descendant.Deleted && descendant.DateUpdated == storedDateUpdated)
                {
                    descendant.Deleted = false;
                    var descendantUrl = descendant.Url();
                    if (!descendant.IsNestedContent && !descendantUrl.IsNullOrWhiteSpace())
                    {
                        if (reservedUrls.Contains(descendantUrl!) || await IsUrlTakenAsync(dbContext, descendantUrl!, descendant.Id, cancellationToken))
                        {
#pragma warning disable CS0618 // Type or member is obsolete
                            descendant.Url = await GenerateUniqueUrlAsync(dbContext, descendantUrl!, cancellationToken, descendant.Id, reservedUrls);
#pragma warning restore CS0618 // Type or member is obsolete
                            await KeepDraftUrlAsync(dbContext, descendant, cancellationToken);
                            handlerResult.AddMessage(
                                $"The URL '{descendantUrl}' is now used by another page, so the restored page '{descendant.Name}' has the URL '{descendant.Url()}'",
                                ResultMessageType.Warning);
                        }

                        reservedUrls.Add(descendant.Url()!);
                    }
                }
            }
        }

        if (validateBlockTypes)
        {
            // Server-side validation of BlockListEditor AllowedElementTypeIds.
            // UI restrictions can be bypassed via a crafted save (or an old payload after settings change),
            // so we re-check here and warn — non-fatal so legacy data isn't blocked from saving.
            await ValidateBlockListAllowedTypesAsync(content, dbContext, handlerResult, cancellationToken);
        }

        return new StagedContent(content, isUpdate);
    }

    /// <summary>
    /// Queries content items with filtering, paging and ordering. Can optionally use cache.
    /// </summary>
    /// <param name="parameters">Query options including filters, includes and paging.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Paged list of content items.</returns>
    public async Task<PaginatedList<Models.Content>> QueryContentAsync(QueryContentParameters parameters,
        CancellationToken cancellationToken = default)
    {
        using var scope = serviceScopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IZauberDbContext>();
        var query = BuildQuery(parameters, dbContext);
        if (parameters.Cached)
        {
            var cacheKey = $"{query.GenerateCacheKey()}_Page{parameters.PageIndex}_Amount{parameters.AmountPerPage}";
            return (await cacheService.GetSetCachedItemAsync(cacheKey,
                async () => await FetchContentAsync(query, parameters, cancellationToken)))!;
        }

        return await FetchContentAsync(query, parameters, cancellationToken);
    }

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
            handlerResult.AddMessage("Unable to delete, as no Content with that id exists", ResultMessageType.Warning);
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
            handlerResult.AddMessage(
                "Unable to delete content with child content, delete or move those items first",
                ResultMessageType.Error);
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
    /// Gives each of <paramref name="copiedItems"/> copies of its blocks: each block its nested content properties
    /// point at is cloned with a new id (and its own blocks with it), and the property values are rewritten to the
    /// clones. Level by level, one query per level (ZA-073). <paramref name="originalToCloneMap"/> is shared by the
    /// whole copy, so a block reached twice gets one clone.
    /// </summary>
    private async Task CloneBlockListContentTreeAsync(IReadOnlyList<Models.Content> copiedItems, NestedContentTree tree,
        IZauberDbContext dbContext, User? currentUser, Dictionary<Guid, Guid> originalToCloneMap,
        CancellationToken cancellationToken)
    {
        var level = copiedItems.ToList();
        for (var depth = 0; level.Count > 0; depth++)
        {
            if (depth >= NestedContentTree.MaxDepth)
            {
                logger.LogWarning("Blocks of copied content are nested deeper than {MaxDepth}; the rest is not copied",
                    NestedContentTree.MaxDepth);
                return;
            }

            var references = new List<(Models.Content Owner, ContentPropertyValue Value,
                NestedContentTree.NestedProperty Property, IReadOnlyList<Guid> Ids)>();
            foreach (var item in level)
            {
                foreach (var (value, property, ids) in await tree.GetReferencesAsync(item, cancellationToken))
                {
                    references.Add((item, value, property, ids));
                }
            }

            var originals = await tree.LoadAsync(references.SelectMany(r => r.Ids)
                .Where(id => !originalToCloneMap.ContainsKey(id)).Distinct().ToList(), cancellationToken);
            var next = new List<Models.Content>();
            foreach (var (owner, value, property, ids) in references)
            {
                foreach (var origId in ids)
                {
                    // Already cloned (another property, another copied item), or not a block: the reference stays
                    if (originalToCloneMap.ContainsKey(origId) ||
                        !originals.TryGetValue(origId, out var original) || !original.IsNestedContent)
                    {
                        continue;
                    }

                    var clone = original.MapToNew();
                    clone.Id = Guid.NewGuid().NewSequentialGuid();
                    clone.DateCreated = DateTime.UtcNow;
                    clone.DateUpdated = DateTime.UtcNow;
                    clone.LastUpdatedById = currentUser?.Id;
                    clone.Deleted = false;
                    clone.RelatedContentId = owner.Id;
                    // Not the original block's draft (ZA-049), and blocks have no URL (ZA-067)
                    clone.UnpublishedContentId = null;
#pragma warning disable CS0618 // Type or member is obsolete
                    clone.Url = null;
#pragma warning restore CS0618 // Type or member is obsolete
                    clone.PropertyData = original.PropertyData.Select(p => new ContentPropertyValue
                    {
                        Id = Guid.NewGuid().NewSequentialGuid(),
                        DateCreated = p.DateCreated,
                        DateUpdated = p.DateUpdated,
                        ContentTypePropertyId = p.ContentTypePropertyId,
                        Value = p.Value,
                        ContentId = clone.Id,
                        Alias = p.Alias
                    }).ToList();

                    // Mapped before the next level, so a cycle in stored data stops at this clone
                    originalToCloneMap[origId] = clone.Id;
                    dbContext.Contents.Add(clone);
                    next.Add(clone);
                }

                value.Value = property.Provider.RemapIds(value.Value, originalToCloneMap) ?? value.Value;
            }

            level = next;
        }
    }

    /// <summary>
    /// Creates a copy of a content item and optionally all descendants. Copies property data and updates paths.
    /// </summary>
    /// <param name="parameters">Copy options including source, destination parent and whether to include descendants.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result including success state and messages.</returns>
    public async Task<HandlerResult<Models.Content>> CopyContentAsync(CopyContentParameters parameters,
        CancellationToken cancellationToken = default)
    {
        using var scope = serviceScopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IZauberDbContext>();
        var user = await currentUserAccessor.GetUserAsync(cancellationToken);
        var handlerResult = new HandlerResult<Models.Content>();

        var contentToCopy = await dbContext.Contents
            .AsNoTracking()
            .Include(content => content.PropertyData)
            .FirstOrDefaultAsync(x => x.Id == parameters.ContentToCopy, cancellationToken);

        if (contentToCopy == null)
        {
            handlerResult.Success = false;
            handlerResult.AddMessage("Unable to copy, as no Content with that ID exists", ResultMessageType.Error);
            return handlerResult;
        }

        var idMap = new Dictionary<Guid, Guid>();
        // URLs handed out in this copy but not saved yet, so two copies never get the same one (ZA-046)
        var reservedUrls = new HashSet<string>(StringComparer.Ordinal);
        var newParentId = parameters.CopyTo ?? contentToCopy.ParentId;
        var copiedContent = await CreateCopyAsync(contentToCopy, user, newParentId);

        idMap[contentToCopy.Id] = copiedContent.Id;

        if (newParentId.HasValue)
        {
            var parentContent = await dbContext.Contents.AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == newParentId, cancellationToken);
            if (parentContent != null)
            {
                copiedContent.Path = [.. parentContent.Path, copiedContent.Id];
            }
        }
        else
        {
            copiedContent.Path = [copiedContent.Id];
        }

        dbContext.Add(copiedContent);

        if (parameters.IncludeDescendants)
        {
            // Walk the tree by ParentId (nearest first, so a parent's copy exists before its children's) rather
            // than matching stored paths with raw SQL. Items in the recycle bin are not copied.
            var copiedPaths = new Dictionary<Guid, List<Guid>> { [copiedContent.Id] = copiedContent.Path };
            // Ids only for the walk; the rows are loaded once below, with their values (ZA-145)
            var descendantIds = await GetDescendantIdsAsync(dbContext, contentToCopy.Id, cancellationToken);
            var treeOrder = descendantIds.Select((id, index) => (id, index)).ToDictionary(x => x.id, x => x.index);
            var descendants = (await dbContext.Contents
                    .AsNoTracking()
                    .Include(content => content.PropertyData)
                    .Where(x => descendantIds.Contains(x.Id))
                    .ToListAsync(cancellationToken))
                .OrderBy(x => treeOrder[x.Id]);

            foreach (var descendant in descendants)
            {
                if (descendant.ParentId == null || descendant.Deleted ||
                    !idMap.TryGetValue(descendant.ParentId.Value, out var newParentIdForDescendant))
                {
                    continue;
                }

                var copiedDescendant = await CreateCopyAsync(descendant, user, newParentIdForDescendant);
                copiedDescendant.Path = [.. copiedPaths[newParentIdForDescendant], copiedDescendant.Id];
                copiedPaths[copiedDescendant.Id] = copiedDescendant.Path;
                idMap[descendant.Id] = copiedDescendant.Id;
                dbContext.Add(copiedDescendant);
            }
        }

        // Deep-copy nested block content referenced by BlockListEditor properties.
        // Without this the copy shares nested rows with the original — editing one mutates the other.
        // Use a single Original→Clone mapping shared across the whole copy operation so a block
        // referenced by multiple properties or multiple descendants resolves to the same new clone.
        var nestedIdMap = new Dictionary<Guid, Guid>();
        var cloneTree = new NestedContentTree(dbContext, extensionManager, logger);
        // The copied page and its copied descendants (tracked: they were added above), taken before any clone is
        // added to the change tracker
        var copiedPages = idMap.Values
            .Select(id => dbContext.Contents.Local.FirstOrDefault(c => c.Id == id))
            .OfType<Models.Content>()
            .ToList();
        await CloneBlockListContentTreeAsync(copiedPages, cloneTree, dbContext, user, nestedIdMap, cancellationToken);
        // The copies' tag links, from the copied values (ZA-099)
        await ContentTagLinks.StageAsync(dbContext, extensionManager,
            dbContext.Contents.Local.Where(c => dbContext.Entry(c).State == EntityState.Added).ToList(),
            cancellationToken);
        AddAuditIfUser(dbContext, user, contentToCopy.Name, "Copied");

        // One save for the copies, their blocks and the audit row; the save hooks see every row as added
        await using (var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken))
        {
            var saved = await dbContext.SaveChangesAndLog([copiedContent], copiedContent, handlerResult, cacheService,
                extensionManager, cancellationToken);
            if (!saved.Success)
            {
                await transaction.RollbackAsync(CancellationToken.None);
                return saved;
            }

            await transaction.CommitAsync(cancellationToken);
            cacheService.ClearCachedItemsWithPrefix(nameof(Models.Content));
        }

        handlerResult.AddMessage("Content copied successfully.", ResultMessageType.Success);
        return handlerResult;

        async Task<Models.Content> CreateCopyAsync(Models.Content original, User? currentUser, Guid? parentId = null)
        {
            var copy = original.MapToNew();
            copy.Id = Guid.NewGuid().NewSequentialGuid();
            copy.Name = original.Name + " (Copy)";
#pragma warning disable CS0618 // Type or member is obsolete
            copy.Url = original.IsNestedContent
                ? null
                : await GenerateUniqueUrlAsync(dbContext,
                    (original.Url().IsNullOrWhiteSpace() ? new SlugHelper().GenerateSlug(original.Name) : original.Url) + "-copy",
                    cancellationToken, reserved: reservedUrls);
#pragma warning restore CS0618 // Type or member is obsolete
            // The original's pending draft belongs to the original; sharing the row breaks both on publish (ZA-049)
            copy.UnpublishedContentId = null;
            copy.LastUpdatedById = currentUser?.Id;
            copy.ParentId = parentId;
            copy.DateCreated = DateTime.UtcNow;
            copy.DateUpdated = DateTime.UtcNow;
            copy.Path = [];
            copy.Published = false;
            copy.Deleted = false;
            copy.PropertyData = original.PropertyData.Select(p => new ContentPropertyValue
            {
                Id = Guid.NewGuid().NewSequentialGuid(),
                DateUpdated = p.DateUpdated,
                DateCreated = p.DateCreated,
                ContentTypePropertyId = p.ContentTypePropertyId,
                Value = p.Value,
                ContentId = p.ContentId,
                Alias = p.Alias
            }).ToList();
            return copy;
        }
    }

    /// <summary>
    /// Resolves content for a frontend request (domain + slug) and builds the corresponding entry model with language.
    /// </summary>
    /// <param name="parameters">Request details such as Url, Slug and child include flags.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Entry model with content and localization information.</returns>
    public async Task<EntryModel> GetContentFromRequestAsync(GetContentFromRequestParameters parameters,
        CancellationToken cancellationToken = default)
    {
        // URLs are stored lower case without slashes around them, so the slug is compared in that form on every
        // database provider (ZA-066): /About-Us/ finds about-us. An empty slug is the root page.
        var slug = ContentUrl.Normalize(parameters.Slug);
        var request = new GetContentFromRequestParameters
        {
            Slug = slug.Length == 0 ? null : slug,
            IsRootContent = parameters.IsRootContent || slug.Length == 0,
            IncludeChildren = parameters.IncludeChildren,
            IgnoreInternalRedirect = parameters.IgnoreInternalRedirect,
            // Only the host and path pick the page (domains match on them): the query string and fragment stay out
            // of the key, or every ?utm_… link would be its own cache entry (ZA-061)
            Url = RequestUrlForLookup(parameters.Url)
        };

        // Cached for up to 5 minutes (it was 5 seconds by mistake, ZA-061). Content, domain, content type, role and
        // language saves clear the Content prefix; the language keys and the default-language fallback are applied below, outside the cache, because
        // they change without a content save.
        // The key changes every EntryCacheMinutes, so an entry lives at most that long even on a page that is hit
        // all the time (the expiry is sliding), and changes the cache isn't told about show within it. A miss (no
        // page) is not kept: its key comes from the visitor's host and path.
        var bucket = DateTime.UtcNow.Ticks / TimeSpan.FromMinutes(EntryCacheMinutes).Ticks;
        var cacheKey = request.GenerateCacheKey<Models.Content>($"GetContentFromRequest-{bucket}");
        var cached = (await cacheService.GetSetCachedItemAsync(cacheKey, async () =>
        {
            using var scope = serviceScopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<IZauberDbContext>();
            return await FetchEntryModelAsync(request, dbContext, cancellationToken);
        }, EntryCacheMinutes, entry => entry.Content != null))!;

        var entryModel = new EntryModel { Content = cached.Content, LanguageIsoCode = cached.LanguageIsoCode };
        if (entryModel.Content == null)
        {
            return entryModel;
        }

        if (entryModel.LanguageIsoCode.IsNullOrWhiteSpace())
        {
            // The site's default language (set by the installer, or in Global Settings); a site that has none keeps
            // the old fallback, the admin's language
            using var settingsScope = serviceScopeFactory.CreateScope();
            var defaultLanguage = (await settingsScope.ServiceProvider.GetRequiredService<Data.Interfaces.IDataService>()
                .GetGlobalSettings()).DefaultLanguage;
            entryModel.LanguageIsoCode = defaultLanguage.IsNullOrWhiteSpace()
                ? settings.Value.AdminDefaultLanguage
                : defaultLanguage;
        }

        var allLanguageData = await languageService.GetCachedAllLanguageDictionariesAsync(
            new ZauberCMS.Core.Languages.Parameters.GetCachedAllLanguageDictionariesParameters(), cancellationToken);
        if (allLanguageData.TryGetValue(entryModel.LanguageIsoCode!, out var languageKeys) && languageKeys != null)
        {
            entryModel.LanguageKeys = languageKeys;
        }

        return entryModel;
    }

    /// <summary>How long a front-end URL → page lookup stays cached (sliding; saves clear it sooner).</summary>
    private const int EntryCacheMinutes = 5;

    /// <summary>Scheme, host, port and path of a request URL, without the query string and fragment.</summary>
    private static string? RequestUrlForLookup(string? url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) ? uri.GetLeftPart(UriPartial.Path).ToLowerInvariant() : url;

    /// <summary>
    /// Retrieves a single content type by id.
    /// </summary>
    /// <param name="parameters">Parameters containing the content type id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The content type or null.</returns>
    public async Task<ContentType?> GetContentTypeAsync(GetContentTypeParameters parameters,
        CancellationToken cancellationToken = default)
    {
        using var scope = serviceScopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IZauberDbContext>();
        return await dbContext.ContentTypes.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == parameters.Id, cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Creates or updates a content type and logs audit entries. Ensures alias uniqueness.
    /// </summary>
    /// <param name="parameters">The content type to save.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result including success state and messages.</returns>
    public async Task<HandlerResult<ContentType>> SaveContentTypeAsync(SaveContentTypeParameters parameters,
        CancellationToken cancellationToken = default)
    {
        using var scope = serviceScopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IZauberDbContext>();
        var user = await currentUserAccessor.GetUserAsync(cancellationToken);
        var handlerResult = new HandlerResult<ContentType>();
        var isUpdate = false;

        if (parameters.ContentType == null)
        {
            handlerResult.AddMessage("ContentType is null", ResultMessageType.Error);
            return handlerResult;
        }

        if (parameters.ContentType.Alias.IsNullOrWhiteSpace())
        {
            parameters.ContentType.Alias = parameters.ContentType.Name.ToAlias();
        }

        // Each page reads its values by alias: two properties with one alias (on the type, or through its
        // compositions) would make one value hide the other (ZA-028). Refused, as Umbraco does.
        var aliasClash = await FindPropertyAliasClashAsync(dbContext, parameters.ContentType, cancellationToken);
        if (aliasClash != null)
        {
            handlerResult.AddMessage(aliasClash, ResultMessageType.Error);
            return handlerResult;
        }

        var contentType = await dbContext.ContentTypes.FirstOrDefaultAsync(x => x.Id == parameters.ContentType.Id, cancellationToken);
        if (contentType == null)
        {
            var containsAlias = await dbContext.ContentTypes.AnyAsync(x => x.Alias == parameters.ContentType.Alias, cancellationToken);
            if (containsAlias)
            {
                handlerResult.AddMessage("Content Type Alias already exists, change the content type name",
                    ResultMessageType.Error);
                return handlerResult;
            }

            contentType = parameters.ContentType;
            contentType.LastUpdatedById = user?.Id;
            dbContext.ContentTypes.Add(contentType);
        }
        else
        {
            isUpdate = true;
            parameters.ContentType.MapTo(contentType);
            contentType.LastUpdatedById = user?.Id;
            contentType.DateUpdated = DateTime.UtcNow;
        }

        var actionText = isUpdate ? "Updated" : "Created";
        AddAuditIfUser(dbContext, user, contentType.Name, actionText);

        var savedType = await dbContext.SaveChangesAndLog(contentType, handlerResult, cacheService, extensionManager,
            cancellationToken);
        if (savedType.Success)
        {
            // Cached front-end pages hold their content type (IncludeChildren, the view)
            cacheService.ClearCachedItemsWithPrefix(nameof(Models.Content));
        }

        return savedType;
    }

    /// <summary>
    /// The error to show when <paramref name="contentType"/> as it is about to be saved would give a page two
    /// properties with one alias (ignoring case): on the type itself, between the type and its compositions, or, for a
    /// composition, on any type that uses it. Null when every alias is unique.
    /// </summary>
    private static async Task<string?> FindPropertyAliasClashAsync(IZauberDbContext dbContext, ContentType contentType,
        CancellationToken cancellationToken)
    {
        // Content types are few and their properties are a JSON column: compare in memory
        var others = await dbContext.ContentTypes.AsNoTracking()
            .Where(x => x.Id != contentType.Id)
            .ToListAsync(cancellationToken);
        var byId = others.ToDictionary(x => x.Id);
        byId[contentType.Id] = contentType;

        var typesToCheck = new List<ContentType> { contentType };
        if (contentType.IsComposition)
        {
            typesToCheck.AddRange(others.Where(x => x.CompositionIds.Contains(contentType.Id)));
        }

        foreach (var type in typesToCheck)
        {
            var sources = new List<ContentType> { type };
            sources.AddRange(type.CompositionIds.Distinct()
                .Where(id => id != type.Id)
                .Select(byId.GetValueOrDefault)
                .OfType<ContentType>());

            var seen = new Dictionary<string, (ContentType Source, Guid PropertyId)>(StringComparer.OrdinalIgnoreCase);
            foreach (var source in sources)
            {
                foreach (var property in source.ContentProperties.Where(p => !p.Alias.IsNullOrWhiteSpace()))
                {
                    var alias = property.Alias!;
                    if (seen.TryAdd(alias, (source, property.Id)))
                    {
                        continue;
                    }

                    var (first, firstPropertyId) = seen[alias];
                    if (firstPropertyId == property.Id)
                    {
                        // The same property reached twice (a copy of a composition's property): not a clash
                        continue;
                    }

                    if (!ReferenceEquals(first, contentType) && !ReferenceEquals(source, contentType))
                    {
                        // A clash this save doesn't touch (between two other types, already stored): not this save's
                        // to refuse, or a composition could never be saved again
                        continue;
                    }

                    return ReferenceEquals(first, source)
                        ? $"Two properties on '{source.Name}' use the alias '{alias}'. Give each property its own alias."
                        : $"The alias '{alias}' is used by '{first.Name}' and '{source.Name}', which are both on " +
                          $"'{type.Name}'. Give each property its own alias.";
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Queries content types with filtering and paging.
    /// </summary>
    /// <param name="parameters">Query options including filters and paging.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Paged list of content types.</returns>
    public async Task<PaginatedList<ContentType>> QueryContentTypesAsync(QueryContentTypesParameters parameters,
        CancellationToken cancellationToken = default)
    {
        using var scope = serviceScopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IZauberDbContext>();

        IQueryable<ContentType> query;

        // Start from external query if provided, otherwise from DbSet
        if (parameters.Query is not null)
        {
            query = parameters.Query.Invoke();
        }
        else
        {
            query = dbContext.ContentTypes.AsQueryable();

            if (parameters.AsNoTracking)
            {
                query = query.AsNoTracking();
            }

            if (parameters.Ids.Count != 0)
            {
                query = query.Where(x => parameters.Ids.Contains(x.Id));
            }

            if (!parameters.SearchTerm.IsNullOrWhiteSpace())
            {
                // LOWER() in SQL on the column, the term lowered here without the server's culture: a culture
                // such as Turkish lowers "I" to "ı" and the search then finds nothing (ZA-017)
                var searchTerm = parameters.SearchTerm.ToLowerInvariant();
#pragma warning disable CA1304, CA1311, CA1862 // EF translates ToLower() on the column to SQL LOWER(); the culture overloads are not translatable
                query = query.Where(x => x.Name != null && x.Name.ToLower().Contains(searchTerm));
#pragma warning restore CA1304, CA1311, CA1862
            }

            if (parameters.OnlyElementTypes)
            {
                query = query.Where(x => x.IsElementType == true);
            }
            else if (!parameters.IncludeElementTypes)
            {
                query = query.Where(x => x.IsElementType == false);
            }

            if (parameters.OnlyCompositions)
            {
                query = query.Where(x => x.IsComposition == true);
            }
            else if (!parameters.IncludeCompositions)
            {
                query = query.Where(x => x.IsComposition == false);
            }

            if (parameters.RootOnly)
            {
                query = query.Where(x => x.AllowAtRoot);
            }

            if (parameters.OnlyFolders)
            {
                query = query.Where(x => x.IsFolder == true);
            }
            else if (!parameters.IncludeFolders)
            {
                query = query.Where(x => x.IsFolder == false);
            }

            if (parameters.ParentId != null)
            {
                query = query.Where(x => x.ParentId == parameters.ParentId);
            }

            if (parameters.WhereClause != null)
            {
                query = query.Where(parameters.WhereClause);
            }

            query = parameters.OrderBy switch
            {
                GetContentTypesOrderBy.DateUpdated => query.OrderBy(p => p.DateUpdated),
                GetContentTypesOrderBy.DateUpdatedDescending => query.OrderByDescending(p => p.DateUpdated),
                GetContentTypesOrderBy.DateCreated => query.OrderBy(p => p.DateCreated),
                GetContentTypesOrderBy.DateCreatedDescending => query.OrderByDescending(p => p.DateCreated),
                GetContentTypesOrderBy.Name => query.OrderBy(p => p.Name),
                _ => query.OrderByDescending(p => p.DateUpdated)
            };
        }

        return await query.ToPaginatedListAsync(parameters.PageIndex, parameters.AmountPerPage, cancellationToken);
    }

    /// <summary>
    /// Deletes a content type if it is unused and has no children. Logs audit entries.
    /// </summary>
    /// <param name="parameters">Parameters containing the content type id to delete.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result including success state and messages.</returns>
    public async Task<HandlerResult<ContentType>> DeleteContentTypeAsync(DeleteContentTypeParameters parameters,
        CancellationToken cancellationToken = default)
    {
        using var scope = serviceScopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IZauberDbContext>();
        var user = await currentUserAccessor.GetUserAsync(cancellationToken);
        var handlerResult = new HandlerResult<ContentType>();

        // Check for regular content using this ContentType: drafts and recycled pages too, because deleting the
        // type cascades to every row that uses it (ZA-070)
        var contentUsingContentType = await dbContext.Contents.AsNoTracking()
            .AnyAsync(x => x.ContentTypeId == parameters.ContentTypeId && !x.IsNestedContent, cancellationToken);
        if (contentUsingContentType)
        {
            handlerResult.Success = false;
            handlerResult.AddMessage("Unable to delete, because this ContentType is being used by content items",
                ResultMessageType.Warning);
            return handlerResult;
        }

        // Check for nested content using this ContentType (Element Types).
        // A nested content row may still exist after its referencing property has been removed
        // from a ContentType (or its composition removed) — the property's JSON value lingers,
        // along with the nested Content row. Treat those as orphans and clean them up so the
        // Element Type can actually be deleted instead of being blocked forever.
        // Every nested row, not one page of published ones (ZA-070)
        var nestedContentUsingContentType = await dbContext.Contents.AsNoTracking()
            .Where(x => x.ContentTypeId == parameters.ContentTypeId && x.IsNestedContent)
            .ToListAsync(cancellationToken);
        if (nestedContentUsingContentType.Count != 0)
        {
            // Build the set of property type IDs that are still attached to some ContentType.
            // ContentProperties is stored as JSON inside ZauberContentTypes, so we materialise
            // the lists in memory and flatten.
            var validPropertyTypeIds = (await dbContext.ContentTypes.AsNoTracking()
                    .Select(ct => ct.ContentProperties)
                    .ToListAsync(cancellationToken))
                .SelectMany(props => props)
                .Select(p => p.Id)
                .ToHashSet();

            var liveItems = new List<Models.Content>();
            var orphanedItems = new List<Models.Content>();

            foreach (var nestedItem in nestedContentUsingContentType)
            {
                var idString = nestedItem.Id.ToString();
                var hasLiveReference = await dbContext.ContentPropertyValues
                    .AnyAsync(pv =>
                            validPropertyTypeIds.Contains(pv.ContentTypePropertyId) &&
                            pv.Value != null && pv.Value.Contains(idString),
                        cancellationToken);

                if (hasLiveReference)
                {
                    // One is enough to refuse; don't scan the property values once per remaining block
                    liveItems.Add(nestedItem);
                    break;
                }
                else
                {
                    orphanedItems.Add(nestedItem);
                }
            }

            if (liveItems.Count != 0)
            {
                handlerResult.Success = false;
                handlerResult.AddMessage(
                    "Unable to delete, because this Element Type is used by nested content",
                    ResultMessageType.Warning);
                return handlerResult;
            }

            // Only orphans remain — purge the dangling rows so the cascade-delete can proceed
            // (Content → ContentType is Cascade, but PropertyData → Content is NoAction, so we
            // have to remove property data and unpublished snapshots ourselves). Re-fetch from
            // the local dbContext because QueryContentAsync above runs in its own scope, so the
            // returned entities are detached relative to this dbContext.
            foreach (var orphanItem in orphanedItems)
            {
                var orphan = await dbContext.Contents.FirstOrDefaultAsync(x => x.Id == orphanItem.Id, cancellationToken);
                if (orphan == null) continue;

                var orphanPropertyValues = dbContext.ContentPropertyValues
                    .Where(pv => pv.ContentId == orphan.Id);
                dbContext.ContentPropertyValues.RemoveRange(orphanPropertyValues);

                if (orphan.UnpublishedContentId != null)
                {
                    var uContent = dbContext.UnpublishedContent
                        .FirstOrDefault(x => x.Id == orphan.UnpublishedContentId);
                    if (uContent != null) dbContext.UnpublishedContent.Remove(uContent);
                }

                dbContext.Contents.Remove(orphan);
            }

            // Their tag links go with them (TagItem has no foreign key to cascade)
            await ContentTagLinks.StageRemoveAsync(dbContext, orphanedItems.Select(x => x.Id).ToList(),
                cancellationToken);
        }

        var children =
            await QueryContentTypesAsync(new QueryContentTypesParameters { ParentId = parameters.ContentTypeId },
                cancellationToken);
        if (children.Items.Any())
        {
            handlerResult.Success = false;
            handlerResult.AddMessage("Unable to delete, because this ContentType has children",
                ResultMessageType.Warning);
            return handlerResult;
        }

        var contentType = await dbContext.ContentTypes.FirstOrDefaultAsync(x => x.Id == parameters.ContentTypeId, cancellationToken);
        if (contentType != null)
        {
            if (contentType.IsComposition)
            {
                var anyUsingThisComposition = await QueryContentTypesForCompositionAsync(dbContext, parameters.ContentTypeId, cancellationToken);
                if (anyUsingThisComposition.Items.Any())
                {
                    handlerResult.Success = false;
                    handlerResult.AddMessage(
                        "Unable to delete, because there are content types using this composition, remove it first",
                        ResultMessageType.Warning);
                    return handlerResult;
                }
            }

            // Orphan clean-up, audit row and delete commit together: a failed delete leaves no audit row
            // claiming it happened (ZA-070)
            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                AddAuditIfUser(dbContext, user, contentType.Name, "Deleted");

                dbContext.ContentTypes.Remove(contentType);
                var deleteResult = await dbContext.SaveChangesAndLog(contentType, handlerResult, cacheService,
                    extensionManager, cancellationToken);
                if (deleteResult.Success)
                {
                    await transaction.CommitAsync(cancellationToken);
                    // Again after the commit: SaveChangesAndLog cleared before it, and a read in between cached the old rows
                    cacheService.ClearCachedItemsWithPrefix(nameof(Models.Content));
                }
                else
                {
                    await transaction.RollbackAsync(cancellationToken);
                }

                return deleteResult;
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        }

        handlerResult.AddMessage("Unable to delete, as no ContentType with that id exists", ResultMessageType.Warning);
        return handlerResult;
    }

    /// <summary>
    /// Retrieves a domain by url or id. Returns an empty domain when not found.
    /// </summary>
    /// <param name="parameters">Query options including url or id, and tracking flag.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A domain instance (empty if not found).</returns>
    public async Task<Domain> GetDomainAsync(GetDomainParameters parameters,
        CancellationToken cancellationToken = default)
    {
        using var scope = serviceScopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IZauberDbContext>();
        var query = dbContext.Domains.AsQueryable();
        if (parameters.AsNoTracking)
        {
            query = query.AsNoTracking();
        }

        if (parameters.Url != null)
        {
            return await query.FirstOrDefaultAsync(x => x.Url == parameters.Url,
                cancellationToken: cancellationToken) ?? new Domain();
        }

        if (parameters.Id != null)
        {
            return await query.FirstOrDefaultAsync(x => x.Id == parameters.Id, cancellationToken: cancellationToken) ??
                   new Domain();
        }

        return await query.FirstOrDefaultAsync(cancellationToken: cancellationToken) ?? new Domain();
    }

    /// <summary>
    /// Creates or updates a domain and logs audit entries.
    /// </summary>
    /// <param name="parameters">The domain to save.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result including success state and messages.</returns>
    public async Task<HandlerResult<Domain>> SaveDomainAsync(SaveDomainParameters parameters,
        CancellationToken cancellationToken = default)
    {
        using var scope = serviceScopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IZauberDbContext>();
        var user = await currentUserAccessor.GetUserAsync(cancellationToken);
        var handlerResult = new HandlerResult<Domain>();
        var isUpdate = false;

        if (parameters.Domain == null)
        {
            handlerResult.AddMessage("Domain is null", ResultMessageType.Error);
            return handlerResult;
        }

        var domain = await dbContext.Domains.FirstOrDefaultAsync(x => x.Id == parameters.Domain.Id, cancellationToken);
        if (domain == null)
        {
            domain = parameters.Domain;
            dbContext.Domains.Add(domain);
        }
        else
        {
            isUpdate = true;
            parameters.Domain.MapTo(domain);
            domain.DateUpdated = DateTime.UtcNow;
        }

        AddAuditIfUser(dbContext, user, $"Domain ({domain.Url})", isUpdate ? "Updated" : "Created");

        var saved = await dbContext.SaveChangesAndLog(domain, handlerResult, cacheService, extensionManager,
            cancellationToken);
        if (saved.Success)
        {
            // Cached front-end lookups (URL → page and language) depend on the domains
            cacheService.ClearCachedItemsWithPrefix(nameof(Models.Content));
        }

        return saved;
    }

    /// <summary>
    /// Queries domains with filtering and paging.
    /// </summary>
    /// <param name="parameters">Query options including ids, where clause and ordering.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Paged list of domains.</returns>
    public async Task<PaginatedList<Domain>> QueryDomainAsync(QueryDomainParameters parameters,
        CancellationToken cancellationToken = default)
    {
        using var scope = serviceScopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IZauberDbContext>();
        var query = dbContext.Domains.AsQueryable();
        
        if (parameters.Query != null)
        {
            query = parameters.Query.Invoke();
        }
        else
        {
            if (parameters.AsNoTracking)
            {
                query = query.AsNoTracking();
            }

            var idCount = parameters.Ids.Count;
            if (idCount != 0)
            {
                query = query.Where(x => parameters.Ids.Contains(x.Id));
                parameters.AmountPerPage = idCount;
            }

            if (parameters.ContentId != null)
            {
                query = query.Where(x => x.ContentId == parameters.ContentId);
            }

            if (parameters.LanguageId != null)
            {
                query = query.Where(x => x.LanguageId == parameters.LanguageId);
            }

            if (parameters.WhereClause != null)
            {
                query = query.Where(parameters.WhereClause);
            }

            query = parameters.OrderBy switch
            {
                GetDomainOrderBy.DateCreated => query.OrderBy(p => p.DateCreated),
                GetDomainOrderBy.DateCreatedDescending => query.OrderByDescending(p => p.DateCreated),
                GetDomainOrderBy.Url => query.OrderBy(p => p.Url),
                _ => query.OrderByDescending(p => p.DateCreated)
            };    
        }
        
        return await query.ToPaginatedListAsync(parameters.PageIndex, parameters.AmountPerPage, cancellationToken);
    }

    /// <summary>
    /// Deletes a domain by id or by content id and logs an audit entry.
    /// </summary>
    /// <param name="parameters">Parameters to identify the domain.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result including success state and messages.</returns>
    public async Task<HandlerResult<Domain?>> DeleteDomainAsync(DeleteDomainParameters parameters,
        CancellationToken cancellationToken = default)
    {
        using var scope = serviceScopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IZauberDbContext>();
        var user = await currentUserAccessor.GetUserAsync(cancellationToken);
        var handlerResult = new HandlerResult<Domain?>();

        Domain? domain = null;
        if (parameters.Id != null)
        {
            domain = await dbContext.Domains.FirstOrDefaultAsync(x => x.Id == parameters.Id,
                cancellationToken: cancellationToken);
        }
        else if (parameters.ContentId != null)
        {
            domain = await dbContext.Domains.FirstOrDefaultAsync(x => x.ContentId == parameters.ContentId,
                cancellationToken: cancellationToken);
        }

        if (domain != null)
        {
            AddAuditIfUser(dbContext, user, $"Domain ({domain.Url})", "Deleted");

            dbContext.Domains.Remove(domain);
            var deleted = await dbContext.SaveChangesAndLog(domain, handlerResult, cacheService, extensionManager,
                cancellationToken);
            if (deleted.Success)
            {
                // Cached front-end lookups (URL → page and language) depend on the domains
                cacheService.ClearCachedItemsWithPrefix(nameof(Models.Content));
            }

            return deleted;
        }

        handlerResult.AddMessage("Unable to delete, as no Domain with that id exists", ResultMessageType.Warning);
        return handlerResult;
    }

    /// <summary>
    /// Checks whether any content exists.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if any content exists.</returns>
    public async Task<bool> AnyContentAsync(CancellationToken cancellationToken = default)
    {
        // Asked on every front-end 404: cached under the Content prefix, which every content save and delete
        // clears (ZA-064). Content in the recycle bin doesn't count.
        return await cacheService.GetSetCachedItemAsync("AnyContent".GenerateCacheKey<Models.Content>(), async () =>
        {
            using var scope = serviceScopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<IZauberDbContext>();
            return await dbContext.Contents.AsNoTracking().AnyAsync(x => !x.Deleted, cancellationToken);
        });
    }

    /// <summary>
    /// Checks whether a content item has child items.
    /// </summary>
    /// <param name="parameters">Parent id and caching flag.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True when child content exists.</returns>
    public async Task<bool> HasChildContentAsync(HasChildContentParameters parameters,
        CancellationToken cancellationToken = default)
    {
        using var scope = serviceScopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IZauberDbContext>();
        if (parameters.Cached)
        {
            var cacheKey = parameters.GenerateCacheKey<Models.Content>("HasChild");
            return await cacheService.GetSetCachedItemAsync(cacheKey,
                async () => await dbContext.Contents.AsNoTracking().AnyAsync(c => c.ParentId == parameters.ParentId && !c.Deleted,
                    cancellationToken: cancellationToken));
        }

        return await dbContext.Contents.AsNoTracking()
            .AnyAsync(c => c.ParentId == parameters.ParentId && !c.Deleted, cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Checks whether a content type has child content types.
    /// </summary>
    /// <param name="parameters">Parent id and caching flag.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True when child content types exist.</returns>
    public async Task<bool> HasChildContentTypeAsync(HasChildContentTypeParameters parameters,
        CancellationToken cancellationToken = default)
    {
        using var scope = serviceScopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IZauberDbContext>();
        if (parameters.Cached)
        {
            var cacheKey = parameters.GenerateCacheKey<ContentType>("HasContentTypeChild");
            return await cacheService.GetSetCachedItemAsync(cacheKey,
                async () => await dbContext.ContentTypes.AsNoTracking().AnyAsync(c => c.ParentId == parameters.ParentId,
                    cancellationToken: cancellationToken));
        }

        return await dbContext.ContentTypes.AsNoTracking()
            .AnyAsync(c => c.ParentId == parameters.ParentId, cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Returns a dictionary mapping content ids and urls to language ISO codes. Uses caching.
    /// </summary>
    /// <param name="parameters">Unused. Reserved for future options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Dictionary of content identifiers to language codes.</returns>
    public async Task<Dictionary<object, string>> GetContentLanguagesAsync(GetContentLanguagesParameters parameters,
        CancellationToken cancellationToken = default)
    {
        using var scope = serviceScopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IZauberDbContext>();
        var query = dbContext.Contents.AsNoTracking()
            .Where(x => !x.Deleted && !x.IsNestedContent)
            .Include(x => x.Language)
#pragma warning disable CS0618 // Type or member is obsolete
            .Select(c => new { c.Id, c.Url, c.Language })
            .Where(x => x.Language != null && x.Url != null);
#pragma warning restore CS0618 // Type or member is obsolete

        var cacheKey = query.GenerateCacheKey<Models.Content>();

        return (await cacheService.GetSetCachedItemAsync(cacheKey, async () =>
        {
            var contentLanguages = await query.ToListAsync(cancellationToken: cancellationToken);
            var dict = new Dictionary<object, string>();
            foreach (var c in contentLanguages)
            {
                // TryAdd: this runs on every front-end request, so one duplicate URL (older data, or two sites
                // with the same slug) must not take every page down (ZA-046). The first page with it wins.
                dict.TryAdd(c.Url!, c.Language!.LanguageIsoCode!);
                dict.TryAdd(c.Id, c.Language!.LanguageIsoCode!);
            }

            return dict;
        }))!;
    }

    /// <summary>
    /// Returns all domains with languages from cache.
    /// </summary>
    /// <param name="parameters">Unused. Reserved for future options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of domains.</returns>
    public async Task<List<Domain>> GetCachedDomainsAsync(CachedDomainsParameters parameters,
        CancellationToken cancellationToken = default)
    {
        using var scope = serviceScopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IZauberDbContext>();
        var query = dbContext.Domains.AsNoTracking().Include(x => x.Language);
        var cacheKey = query.GenerateCacheKey();
        return (await cacheService.GetSetCachedItemAsync(cacheKey,
            async () => await query.ToListAsync(cancellationToken: cancellationToken)))!;
    }

    /// <summary>
    /// Clears the unpublished content record for a content item, if it exists.
    /// </summary>
    /// <param name="parameters">The content id to clear unpublished content for.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result including success and messages.</returns>
    public async Task<HandlerResult<UnpublishedContent>> ClearUnpublishedContentAsync(
        ClearUnpublishedContentParameters parameters, CancellationToken cancellationToken = default)
    {
        using var scope = serviceScopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IZauberDbContext>();
        var handlerResult = new HandlerResult<UnpublishedContent>();
        var content = await dbContext.Contents.FirstOrDefaultAsync(x => x.Id == parameters.ContentId,
            cancellationToken: cancellationToken);
        if (content?.UnpublishedContentId != null)
        {
            // The draft's blocks go with it: blocks only the draft reaches are deleted, and the live blocks' own
            // drafts are dropped, so the content is again what the live site shows
            var tree = new NestedContentTree(dbContext, extensionManager, logger);
            var stored = (await tree.LoadAsync([content.Id], cancellationToken))[content.Id];
            var live = await tree.WalkAsync(stored, null, useStoredDrafts: false, cancellationToken);
            var draft = await tree.WalkAsync(NestedContentTree.WithStoredDraft(stored), null, useStoredDrafts: true,
                cancellationToken);
            await tree.StageDiscardDraftsAsync(live.Ids, cancellationToken);
            await tree.StageDeleteAsync(draft.Ids.Where(id => !live.Ids.Contains(id)).ToList(), cancellationToken);

            var uContent =
                await dbContext.UnpublishedContent.FirstOrDefaultAsync(x => x.Id == content.UnpublishedContentId,
                    cancellationToken: cancellationToken);
            if (uContent != null) dbContext.UnpublishedContent.Remove(uContent);
            content.UnpublishedContentId = null;

            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
            var result = (await dbContext.SaveChangesAndLog(uContent, handlerResult, cacheService, extensionManager,
                cancellationToken))!;
            if (result.Success)
            {
                await transaction.CommitAsync(cancellationToken);
                cacheService.ClearCachedItemsWithPrefix(nameof(Models.Content));
            }
            else
            {
                await transaction.RollbackAsync(cancellationToken);
            }

            return result;
        }

        return handlerResult;
    }

    /// <summary>
    /// Returns content for a data grid with server-side filtering, ordering and paging.
    /// </summary>
    /// <param name="parameters">Grid options including filter, order and paging.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Data grid result with total count and items.</returns>
    public async Task<DataGridResult<Models.Content>> GetDataGridContentAsync(DataGridContentParameters parameters,
        CancellationToken cancellationToken = default)
    {
        using var scope = serviceScopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IZauberDbContext>();
        var result = new DataGridResult<Models.Content>();
        var query = dbContext.Contents
            .Include(x => x.ContentType)
            .Include(x => x.LastUpdatedBy)
            .Where(x => x.Deleted == false && !x.IsNestedContent)
            .AsQueryable();

        if (parameters.IncludeChildren)
        {
            query = query.Include(x => x.Children.Where(c => !c.Deleted)).AsSplitQuery();
        }

        if (parameters.AsNoTracking)
        {
            query = query.AsNoTracking();
        }

        if (!parameters.ContentTypeAlias.IsNullOrWhiteSpace())
        {
            var contentType = await dbContext.ContentTypes.AsNoTracking()
                .FirstOrDefaultAsync(x => x.Alias == parameters.ContentTypeAlias, cancellationToken);
            if (contentType != null)
            {
                parameters.ContentTypeId = contentType.Id;
            }
        }

        if (parameters.LastEditedBy != null)
        {
            query = query.Where(x => x.LastUpdatedById == parameters.LastEditedBy.Value);
        }

        if (parameters.ContentTypeId != null)
        {
            query = query.Where(x => x.ContentTypeId == parameters.ContentTypeId);
        }

        if (parameters.ParentId != null)
        {
            query = query.Where(x => x.ParentId == parameters.ParentId);
        }

        // The strings are parsed into expressions; GridQueryGuard refuses anything but ordinary column use.
        query = GridQueryGuard.Filter(query, parameters.Filter);

        if (!string.IsNullOrEmpty(parameters.Order))
        {
            query = GridQueryGuard.OrderBy(query, parameters.Order);
        }
        else
        {
            query = parameters.OrderBy switch
            {
                GetContentsOrderBy.DateUpdated => query.OrderBy(p => p.DateUpdated),
                GetContentsOrderBy.DateUpdatedDescending => query.OrderByDescending(p => p.DateUpdated),
                GetContentsOrderBy.DateCreated => query.OrderBy(p => p.DateCreated),
                GetContentsOrderBy.DateCreatedDescending => query.OrderByDescending(p => p.DateCreated),
                GetContentsOrderBy.SortOrder => query.OrderBy(p => p.SortOrder),
                _ => query.OrderByDescending(p => p.DateUpdated)
            };
        }

        result.Count = await query.CountAsync(cancellationToken);
        result.Items = await query.Skip(parameters.Skip).Take(parameters.Take)
            .ToListAsync(cancellationToken: cancellationToken);
        return result;
    }

    public async Task<string?> ExportContentTypeAsync(string alias, bool includeContent = false,
        CancellationToken cancellationToken = default)
    {
        using var scope = serviceScopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IZauberDbContext>();

        // Tabs and properties are columns of the type (JSON), not navigations: an Include of them threw
        var contentType = await dbContext.ContentTypes.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Alias == alias, cancellationToken);

        if (contentType == null) return null;

        var exportType = new ContentTypeExport
        {
            Name = contentType.Name ?? string.Empty,
            Description = contentType.Description ?? string.Empty,
            Alias = contentType.Alias ?? string.Empty,
            Icon = contentType.Icon ?? "description",
            IsElementType = contentType.IsElementType,
            AllowAtRoot = contentType.AllowAtRoot,
            EnableListView = contentType.EnableListView,
            IncludeChildren = contentType.IncludeChildren,
            AvailableContentViews = contentType.AvailableContentViews,
            Tabs = contentType.Tabs,
            ContentProperties = contentType.ContentProperties,
            IsFolder = contentType.IsFolder,
            IsComposition = contentType.IsComposition,
            MediaIdAsString = contentType.MediaIdAsString ?? string.Empty
        };

        // Map CompositionIds to aliases
        if (contentType.CompositionIds.Count != 0)
        {
            exportType.CompositionAliases = await dbContext.ContentTypes
                .Where(ct => contentType.CompositionIds.Contains(ct.Id))
                .Select(ct => ct.Alias!)
                .ToListAsync(cancellationToken);
        }

        // Map AllowedChildContentTypes to aliases
        if (contentType.AllowedChildContentTypes.Count != 0)
        {
            exportType.AllowedChildContentTypeAliases = await dbContext.ContentTypes
                .Where(ct => contentType.AllowedChildContentTypes.Contains(ct.Id))
                .Select(ct => ct.Alias!)
                .ToListAsync(cancellationToken);
        }

        // Map ParentId to alias
        if (contentType.ParentId.HasValue)
        {
            var parent = await dbContext.ContentTypes.FindAsync([contentType.ParentId.Value], cancellationToken);
            exportType.ParentAlias = parent?.Alias ?? string.Empty;
        }

        var package = new ContentTypePackage { Type = exportType };

        if (includeContent)
        {
            var rootContents = await dbContext.Contents
                .Where(c => c.ContentTypeId == contentType.Id && c.ParentId == null)
                .Include(c => c.PropertyData)
                .ToListAsync(cancellationToken);

            package.RootContents = await BuildContentExportTree(rootContents, dbContext, contentType, cancellationToken);
        }

        return JsonSerializer.Serialize(package, ExportJsonOptions);
    }

    private static readonly JsonSerializerOptions ExportJsonOptions = new() { WriteIndented = true };

    private static async Task<List<ContentExport>> BuildContentExportTree(List<Models.Content> contents,
        IZauberDbContext dbContext, ContentType contentType, CancellationToken cancellationToken)
    {
        var exports = new List<ContentExport>();

        foreach (var content in contents)
        {
            var exp = new ContentExport
            {
                Name = content.Name ?? string.Empty,
#pragma warning disable CS0618 // Type or member is obsolete
                Url = content.Url ?? string.Empty,
#pragma warning restore CS0618 // Type or member is obsolete
                ContentTypeAlias = content.ContentTypeAlias ?? string.Empty,
                Published = content.Published,
                Deleted = content.Deleted,
                HideFromNavigation = content.HideFromNavigation,
                InternalRedirectIdAsString = content.InternalRedirectIdAsString ?? string.Empty,
                SortOrder = content.SortOrder,
                ViewComponent = content.ViewComponent,
                LanguageIsoCode = content.Language?.LanguageIsoCode ?? string.Empty,
                // PropertyData
                PropertyData = new Dictionary<string, string>()
            };

            foreach (var pd in content.PropertyData)
            {
                var propType = contentType.ContentProperties.FirstOrDefault(p => p.Id == pd.ContentTypePropertyId);
                if (propType != null)
                {
                    exp.PropertyData[propType.Alias ?? propType.Name ?? string.Empty] = pd.Value;
                }
            }

            // Children recursively
            var children = await dbContext.Contents
                .Where(c => c.ParentId == content.Id)
                .Include(c => c.PropertyData)
                .ToListAsync(cancellationToken);
            exp.Children = await BuildContentExportTree(children, dbContext, contentType, cancellationToken);

            exports.Add(exp);
        }

        return exports;
    }

    public async Task<HandlerResult<ContentType>> ImportContentTypeAsync(string json,
        CancellationToken cancellationToken = default)
    {
        var handlerResult = new HandlerResult<ContentType>();
        var package = JsonSerializer.Deserialize<ContentTypePackage>(json);

        if (package?.Type == null)
        {
            handlerResult.AddMessage("Invalid JSON", ResultMessageType.Error);
            return handlerResult;
        }

        using var scope = serviceScopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IZauberDbContext>();
        var user = await currentUserAccessor.GetUserAsync(cancellationToken);

        // Import ContentType
        var export = package.Type;
        // Not tracked: the type is saved through SaveContentTypeAsync, the content below on this context
        var existing = await dbContext.ContentTypes.AsNoTracking().FirstOrDefaultAsync(x => x.Alias == export.Alias, cancellationToken);

        var contentType = existing ?? new ContentType { Alias = export.Alias };

        contentType.Name = export.Name;
        contentType.Description = export.Description;
        contentType.Icon = export.Icon;
        contentType.IsElementType = export.IsElementType;
        contentType.AllowAtRoot = export.AllowAtRoot;
        contentType.EnableListView = export.EnableListView;
        contentType.IncludeChildren = export.IncludeChildren;
        contentType.AvailableContentViews = export.AvailableContentViews;
        contentType.IsFolder = export.IsFolder;
        contentType.IsComposition = export.IsComposition;
        contentType.MediaIdAsString = export.MediaIdAsString;

        // Tabs and properties the type already has keep their ids, matched by alias: stored values point at the
        // property id, so a new id would leave every value behind on the next save (ZA-077). Anything new gets a new
        // id, so the same package imported into two sites can't clash with ids already used there. The system tab
        // keeps its fixed id.
        var existingTabIds = (existing?.Tabs ?? [])
            .GroupBy(t => t.Alias, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().Id, StringComparer.OrdinalIgnoreCase);
        var existingPropertyIds = (existing?.ContentProperties ?? [])
            .GroupBy(p => p.Alias ?? p.Name ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().Id, StringComparer.OrdinalIgnoreCase);

        var tabMap = new Dictionary<string, Guid>();
        foreach (var tab in export.Tabs)
        {
            // Each kept id once: a second tab or property with the same alias gets a new one
            var newId = tab.IsSystemTab
                ? tab.Id
                : existingTabIds.Remove(tab.Alias, out var keptTabId) ? keptTabId : Guid.NewGuid().NewSequentialGuid();
            tabMap[tab.Alias] = newId;
            tab.Id = newId;
        }

        contentType.Tabs = export.Tabs;

        // Set PropertyType TabIds based on map, keep or make ids, create alias map
        var propMap = new Dictionary<string, Guid>();
        foreach (var prop in export.ContentProperties)
        {
            var key = prop.Alias ?? prop.Name ?? string.Empty;
            var newId = existingPropertyIds.Remove(key, out var keptId) ? keptId : Guid.NewGuid().NewSequentialGuid();
            prop.Id = newId;
            propMap[key] = newId;
            if (!string.IsNullOrEmpty(prop.TabAlias) && tabMap.TryGetValue(prop.TabAlias, out var newTabId))
            {
                prop.TabId = newTabId;
            }
        }

        contentType.ContentProperties = export.ContentProperties;

        // Map CompositionAliases to Ids
        contentType.CompositionIds = [];
        foreach (var compAlias in export.CompositionAliases)
        {
            var comp = await dbContext.ContentTypes.FirstOrDefaultAsync(x => x.Alias == compAlias, cancellationToken);
            if (comp != null)
            {
                contentType.CompositionIds.Add(comp.Id);
            }
            else
            {
                handlerResult.AddMessage($"Composition {compAlias} not found", ResultMessageType.Warning);
            }
        }

        // Map AllowedChildContentTypeAliases to Ids
        contentType.AllowedChildContentTypes = new List<Guid>();
        foreach (var childAlias in export.AllowedChildContentTypeAliases)
        {
            var child = await dbContext.ContentTypes.FirstOrDefaultAsync(x => x.Alias == childAlias, cancellationToken);
            if (child != null)
            {
                contentType.AllowedChildContentTypes.Add(child.Id);
            }
            else
            {
                handlerResult.AddMessage($"Child type {childAlias} not found", ResultMessageType.Warning);
            }
        }

        // Map ParentAlias to Id
        if (!string.IsNullOrEmpty(export.ParentAlias))
        {
            var parent = await dbContext.ContentTypes.FirstOrDefaultAsync(x => x.Alias == export.ParentAlias, cancellationToken);
            if (parent != null)
            {
                contentType.ParentId = parent.Id;
            }
            else
            {
                handlerResult.AddMessage($"Parent {export.ParentAlias} not found", ResultMessageType.Warning);
            }
        }

        var saveParams = new SaveContentTypeParameters { ContentType = contentType };
        var typeResult = await SaveContentTypeAsync(saveParams, cancellationToken);
        if (!typeResult.Success)
        {
            return typeResult;
        }

        // If no content, done
        if (package.RootContents.Count == 0)
        {
            return typeResult;
        }

        // Import content recursively
        foreach (var rootExp in package.RootContents)
        {
            await ImportContentRecursive(rootExp, null, contentType, dbContext, user, propMap, handlerResult);
        }

        // One save for the imported content and its tag links, with the save hooks. The content type is saved by
        // now, so its content is saved even when the caller has gone: an import is not left half done
        await ContentTagLinks.StageAsync(dbContext, extensionManager, dbContext.Contents.Local.ToList(),
            CancellationToken.None);
        var contentResult = await dbContext.SaveChangesAndLog(Array.Empty<Models.Content>(), null,
            new HandlerResult<Models.Content>(), cacheService, extensionManager, CancellationToken.None);
        if (!contentResult.Success)
        {
            typeResult.Success = false;
            typeResult.Messages.AddRange(contentResult.Messages);
        }

        return typeResult;
    }

    private static async Task ImportContentRecursive(ContentExport exp, Guid? parentId, ContentType contentType,
        IZauberDbContext dbContext, User? user, Dictionary<string, Guid> propMap,
        HandlerResult<ContentType> handlerResult)
    {
        var content = new Models.Content
        {
            Name = exp.Name,
#pragma warning disable CS0618 // Type or member is obsolete
            Url = exp.Url, // May need uniqueness check
#pragma warning restore CS0618 // Type or member is obsolete
            ContentTypeId = contentType.Id,
            ContentTypeAlias = contentType.Alias,
            Published = exp.Published,
            Deleted = exp.Deleted,
            HideFromNavigation = exp.HideFromNavigation,
            InternalRedirectIdAsString = exp.InternalRedirectIdAsString,
            SortOrder = exp.SortOrder,
            ViewComponent = exp.ViewComponent,
            ParentId = parentId,
            // Nobody signed in records no user (D27); Guid.Empty would point at no account
            LastUpdatedById = user?.Id,
            DateCreated = DateTime.UtcNow,
            DateUpdated = DateTime.UtcNow
        };

        // Language
        if (!string.IsNullOrEmpty(exp.LanguageIsoCode))
        {
            var language = await dbContext.Languages.FirstOrDefaultAsync(l => l.LanguageIsoCode == exp.LanguageIsoCode);
            if (language != null)
            {
                content.LanguageId = language.Id;
            }
            else
            {
                handlerResult.AddMessage($"Language {exp.LanguageIsoCode} not found for content {exp.Name}",
                    ResultMessageType.Warning);
            }
        }

        // PropertyData
        content.PropertyData = new List<ContentPropertyValue>();
        foreach (var kvp in exp.PropertyData)
        {
            if (propMap.TryGetValue(kvp.Key, out var propId))
            {
                content.PropertyData.Add(new ContentPropertyValue
                {
                    ContentTypePropertyId = propId,
                    Value = kvp.Value,
                    Alias = kvp.Key // Optional
                });
            }
            else
            {
                handlerResult.AddMessage($"Property {kvp.Key} not found for content {exp.Name}",
                    ResultMessageType.Warning);
            }
        }

        dbContext.Contents.Add(content);

        // Build Path after save, but since we're saving at the end, might need to update later

        // Recurse children
        foreach (var childExp in exp.Children)
        {
            await ImportContentRecursive(childExp, content.Id, contentType, dbContext, user, propMap, handlerResult);
        }
    }


    private static IQueryable<Models.Content> BuildQuery(GetContentParameters request, IZauberDbContext dbContext)
    {
        var query = dbContext.Contents
            .Include(x => x.ContentType)
            .Include(x => x.PropertyData)
            .AsSplitQuery()
            .AsQueryable();

        if (request.AsNoTracking)
        {
            query = query.AsNoTracking();
        }

        if (!request.IncludeUnpublished)
        {
            query = query.Where(x => x.Published);
        }

        if (!request.IncludeDeleted)
        {
            query = query.Where(x => !x.Deleted);
        }

        if (request.IncludeUnpublishedContent)
        {
            query = query.Include(x => x.UnpublishedContent);
        }

        if (request.IncludeParent)
        {
            query = query.Include(x => x.Parent);
        }

        if (request.IncludeChildren)
        {
            // Both Published and Deleted filters apply to included children — soft-deleted
            // children must not bleed into the navigation tree under any caller.
            query = request.IncludeUnpublished
                ? query.Include(x => x.Children.Where(c => !c.Deleted))
                : query.Include(x => x.Children.Where(c => c.Published && !c.Deleted));
            query = query.AsSplitQuery();
        }

        if (request.IncludeContentRoles)
        {
            query = query.Include(x => x.ContentRoles).ThenInclude(x => x.Role).AsSplitQuery();
        }

        if (request.Id != null)
        {
            return query.Where(x => x.Id == request.Id);
        }

        if (!request.ContentTypeAlias.IsNullOrWhiteSpace())
        {
            return query.Where(x => x.ContentType != null && x.ContentType.Alias == request.ContentTypeAlias);
        }

        return query;
    }

    private static IQueryable<Models.Content> BuildQuery(QueryContentParameters request, IZauberDbContext dbContext)
    {
        var query = dbContext.Contents.Include(x => x.ContentType)
            .Include(x => x.PropertyData).AsSplitQuery().AsQueryable();

        if (request.Query != null)
        {
            query = request.Query.Invoke();
        }
        else
        {
            if (request.IncludeContentRoles)
            {
                query = query.Include(x => x.ContentRoles);
            }

            if (request.OnlyUnpublished)
            {
                query = query.Include(x => x.UnpublishedContent);
                query = query.Where(x => x.UnpublishedContentId != null || x.Published == false);
            }
            else
            {
                query = !request.IncludeUnpublished
                    ? query.Where(x => x.Published)
                    : query.Include(x => x.UnpublishedContent);
            }

            if (request.OnlyDeleted)
            {
                query = query.Where(x => x.Deleted);
            }
            else if (!request.IncludeDeleted)
            {
                query = query.Where(x => !x.Deleted);
            }

            if (request.IncludeChildren)
            {
                // Recycled children are never included, whatever the deleted flags ask of the items themselves (ZA-065)
                query = request.IncludeUnpublished
                    ? query.Include(x => x.Children.Where(c => !c.Deleted))
                        .ThenInclude(x => x.UnpublishedContent)
                    : query.Include(x => x.Children.Where(c => c.Published && !c.Deleted));
            }

            if (request.RootContentOnly)
            {
                query = query.Where(x => x.ParentId == null);
            }

            if (request.TagSlugs.Count != 0)
            {
                query = (from content in query
                        join tagItem in dbContext.TagItems on content.Id equals tagItem.ItemId
                        join tag in dbContext.Tags on tagItem.TagId equals tag.Id
                        where request.TagSlugs.Contains(tag.Slug)
                        select content)
                    .Distinct();
            }

            if (request.AsNoTracking)
            {
                query = query.AsNoTracking();
            }

            if (!string.IsNullOrWhiteSpace(request.SearchTerm))
            {
                // LOWER() in SQL on the column, the term lowered here without the server's culture: a culture
                // such as Turkish lowers "I" to "ı" and the search then finds nothing (ZA-017)
                var searchTerm = request.SearchTerm.ToLowerInvariant();
#pragma warning disable CA1304, CA1311, CA1862 // EF translates ToLower() on the column to SQL LOWER(); the culture overloads are not translatable
                query = query.Where(x => x.Name != null && x.Name.ToLower().Contains(searchTerm));
#pragma warning restore CA1304, CA1311, CA1862
            }

            if (!string.IsNullOrWhiteSpace(request.ContentTypeAlias))
            {
                // Filtered in SQL: looking the type up first was an extra, uncached query before the cache check
                // on every call (ZA-062). The alias wins over ContentTypeId, as before.
                var contentTypeAlias = request.ContentTypeAlias;
                query = query.Where(x => x.ContentType != null && x.ContentType.Alias == contentTypeAlias);
            }
            else if (request.ContentTypeId != null)
            {
                query = query.Where(x => x.ContentTypeId == request.ContentTypeId);
            }

            if (request.ParentId != null)
            {
                query = query.Where(x => x.ParentId == request.ParentId);
            }

            var idCount = request.Ids.Count;
            if (idCount != 0)
            {
                query = query.Where(x => request.Ids.Contains(x.Id));
                request.AmountPerPage = idCount;
            }

            query = ApplyNestedFilter(query, request.NestedFilter);
        }

        if (request.WhereClause != null)
        {
            query = query.Where(request.WhereClause);
        }

        query = request.OrderBy switch
        {
            GetContentsOrderBy.DateUpdated => query.OrderBy(p => p.DateUpdated),
            GetContentsOrderBy.DateUpdatedDescending => query.OrderByDescending(p => p.DateUpdated),
            GetContentsOrderBy.DateCreated => query.OrderBy(p => p.DateCreated),
            GetContentsOrderBy.DateCreatedDescending => query.OrderByDescending(p => p.DateCreated),
            GetContentsOrderBy.SortOrder => query.OrderBy(p => p.SortOrder),
            _ => query.OrderByDescending(p => p.DateUpdated)
        };

        // Pages with the same date or sort order keep their place from one page of results to the next (ZA-115)
        return ((IOrderedQueryable<Models.Content>)query).ThenBy(p => p.Id);
    }

    private static async Task<PaginatedList<Models.Content>> FetchContentAsync(IQueryable<Models.Content> query,
        QueryContentParameters request, CancellationToken cancellationToken)
    {
        return await query.ToPaginatedListAsync(request.PageIndex, request.AmountPerPage, cancellationToken);
    }

    private static async Task<Models.Content?> FetchContentAsync(IQueryable<Models.Content> query,
        CancellationToken cancellationToken)
    {
        return await query.FirstOrDefaultAsync(cancellationToken: cancellationToken);
    }

    private async Task<EntryModel> FetchEntryModelAsync(GetContentFromRequestParameters request,
        IZauberDbContext dbContext, CancellationToken cancellationToken)
    {
        var entryModel = new EntryModel();
        var contentQueryable = dbContext.Contents.AsNoTracking().Include(x => x.ContentType);

        var domains = await GetCachedDomainsAsync(new CachedDomainsParameters(), cancellationToken);
        var contentWithLanguages =
            await GetContentLanguagesAsync(new GetContentLanguagesParameters(), cancellationToken);

        var matchedDomain = MatchDomainWithContent(request.Url ?? string.Empty, domains);

        // Only live pages are served: published, not in the recycle bin, and not a block (ZA-065, ZA-067)
        var liveContent = contentQueryable.Where(c => c.Published && !c.Deleted && !c.IsNestedContent);
        var content = request.IsRootContent
            ? matchedDomain != null
                ? await liveContent.Select(c => new
                        { c.Id, c.InternalRedirectId, c.ContentType!.IncludeChildren, c.Path })
                    .FirstOrDefaultAsync(x => x.Id == matchedDomain.ContentId, cancellationToken)
                // The first root page in tree order, the same one every time (ZA-115)
                : await liveContent.Where(c => c.IsRootContent)
                    .OrderBy(c => c.SortOrder).ThenBy(c => c.Id)
                    .Select(c => new { c.Id, c.InternalRedirectId, c.ContentType!.IncludeChildren, c.Path })
                    .FirstOrDefaultAsync(cancellationToken)
#pragma warning disable CS0618 // Type or member is obsolete
            : await liveContent.Where(c => c.Url == request.Slug)
#pragma warning restore CS0618 // Type or member is obsolete
                .OrderBy(c => c.SortOrder).ThenBy(c => c.Id)
                .Select(c => new { c.Id, c.InternalRedirectId, c.ContentType!.IncludeChildren, c.Path })
                .FirstOrDefaultAsync(cancellationToken);

        if (content?.InternalRedirectId != null && content.InternalRedirectId != Guid.Empty)
        {
            var internalRedirectIdValue = content.InternalRedirectId.Value;
            content = await liveContent.Where(c => c.Id == internalRedirectIdValue)
                .Select(c => new { c.Id, c.InternalRedirectId, c.ContentType!.IncludeChildren, c.Path })
                .FirstOrDefaultAsync(cancellationToken);
        }

        if (content == null)
        {
            return entryModel;
        }

        var query = dbContext.Contents.AsNoTracking().AsSplitQuery()
            .Include(x => x.PropertyData)
            .Include(x => x.Parent)
            .Include(x => x.ContentType)
            .Include(x => x.Language)
            .Include(x => x.ContentRoles).ThenInclude(x => x.Role)
            .AsQueryable();

        if (request.IncludeChildren || content.IncludeChildren)
        {
            // A view lists its children: only the ones a visitor may see (ZA-065)
            query = query.Include(x => x.Children.Where(c => c.Published && !c.Deleted && !c.IsNestedContent));
        }

        var fullContent =
            await query.FirstOrDefaultAsync(c => c.Id == content.Id, cancellationToken: cancellationToken);
        entryModel.Content = fullContent;

        string? languageIsoCode = null;
        if (matchedDomain?.Language?.LanguageIsoCode != null)
        {
            languageIsoCode = matchedDomain.Language.LanguageIsoCode;
        }
        else if (contentWithLanguages.TryGetValue(request.Slug ?? string.Empty, out var contentLanguage))
        {
            languageIsoCode = contentLanguage;
        }

        if (languageIsoCode.IsNullOrWhiteSpace())
        {
            foreach (var guid in content.Path)
            {
                if (contentWithLanguages.TryGetValue(guid, out var language))
                {
                    languageIsoCode = language;
                    break;
                }
            }
        }

        // Null when neither a domain nor the page or an ancestor sets one: the caller falls back to the default
        entryModel.LanguageIsoCode = languageIsoCode.IsNullOrWhiteSpace() ? null : languageIsoCode;
        return entryModel;
    }

    private static Domain? MatchDomainWithContent(string url, List<Domain> domains)
    {
        // A caller without a full URL (code, a test) matches no domain instead of throwing
        if (domains.Count == 0 || !Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return null;
        }

        // The same rule the redirect middleware uses for a redirect's domain (ZA-165)
        return DomainMatching.FindDomain(domains, uri.Host, uri.AbsolutePath);
    }

    /// <summary>
    /// <paramref name="baseSlug"/>, or it with the first free <c>-1</c>, <c>-2</c>… suffix. Taken means used by
    /// another page that is not in the recycle bin (blocks have no URL), or in <paramref name="reserved"/>: URLs
    /// handed out earlier in the same operation and not saved yet.
    /// </summary>
    private static async Task<string> GenerateUniqueUrlAsync(IZauberDbContext dbContext, string baseSlug,
        CancellationToken cancellationToken, Guid? ownId = null, HashSet<string>? reserved = null)
    {
        // One query for the URL and its numbered forms: this asked the database once for every number (ZA-115)
        var taken = await TakenUrlsAsync(dbContext, baseSlug, ownId, cancellationToken);
        var url = baseSlug;
        var counter = 1;
        while ((reserved?.Contains(url) ?? false) || taken.Contains(url))
        {
            url = $"{baseSlug}-{counter}";
            counter++;
        }

        reserved?.Add(url);
        return url;
    }

    /// <summary>True when a page other than <paramref name="ownId"/> that is not in the recycle bin has this URL.</summary>
    private static async Task<bool> IsUrlTakenAsync(IZauberDbContext dbContext, string url, Guid? ownId,
        CancellationToken cancellationToken)
    {
        var id = ownId ?? Guid.Empty;
#pragma warning disable CS0618 // Type or member is obsolete
        return await dbContext.Contents.AsNoTracking()
            .AnyAsync(c => c.Url == url && !c.Deleted && !c.IsNestedContent && c.Id != id, cancellationToken);
#pragma warning restore CS0618 // Type or member is obsolete
    }

    /// <summary>
    /// The URLs <paramref name="baseSlug"/> and <c>baseSlug-…</c> used by pages other than <paramref name="ownId"/>
    /// that are not in the recycle bin.
    /// </summary>
    private static async Task<HashSet<string>> TakenUrlsAsync(IZauberDbContext dbContext, string baseSlug,
        Guid? ownId, CancellationToken cancellationToken)
    {
        var id = ownId ?? Guid.Empty;
        var numbered = baseSlug + "-";
#pragma warning disable CS0618 // Type or member is obsolete
        var urls = await dbContext.Contents.AsNoTracking()
            .Where(c => (c.Url == baseSlug || c.Url!.StartsWith(numbered)) && !c.Deleted && !c.IsNestedContent &&
                        c.Id != id)
            .Select(c => c.Url!)
            .ToListAsync(cancellationToken);
#pragma warning restore CS0618 // Type or member is obsolete
        // Stored URLs are lower case (ContentUrl.Normalize), so letter case only matters for rows from before that;
        // ignoring it here never hands out a URL that differs from another page's by case alone
        return urls.ToHashSet(StringComparer.OrdinalIgnoreCase);
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

    /// <summary>
    /// Gives the item's pending draft the URL a restore had to change: the editor shows the draft's URL, so the
    /// draft would otherwise be refused on publish for a URL another page now has (ZA-144).
    /// </summary>
    private static async Task KeepDraftUrlAsync(IZauberDbContext dbContext, Models.Content item,
        CancellationToken cancellationToken)
    {
        if (item.UnpublishedContentId is not { } draftId) return;
        var draft = await dbContext.UnpublishedContent.FirstOrDefaultAsync(x => x.Id == draftId, cancellationToken);
        if (draft == null) return;
#pragma warning disable CS0618 // Type or member is obsolete
        draft.JsonContent.Url = item.Url;
#pragma warning restore CS0618 // Type or member is obsolete
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

    private static async Task UpdateContentRolesAsync(IZauberDbContext dbContext, Models.Content content,
        SaveContentParameters request, CancellationToken cancellationToken)
    {
        var existingRoles = await dbContext.ContentRoles.AsNoTracking().Where(r => r.ContentId == content.Id)
            .ToListAsync(cancellationToken);
        var rolesToRemove = existingRoles.Where(er => request.Roles.All(rr => rr.Id != er.RoleId)).ToList();
        if (rolesToRemove.Count != 0)
        {
            dbContext.ContentRoles.RemoveRange(rolesToRemove);
        }

        var rolesToAdd = request.Roles.Where(rr => existingRoles.All(er => er.RoleId != rr.Id)).ToList();
        if (rolesToAdd.Count != 0)
        {
            foreach (var role in rolesToAdd)
            {
                var contentRole = new ContentRole { ContentId = content.Id, RoleId = role.Id };
                dbContext.ContentRoles.Add(contentRole);
            }
        }
    }

    private static async Task<int> GetNextSortOrderAsync(IZauberDbContext dbContext, Guid? parentId, bool isRootContent, CancellationToken cancellationToken)
    {
        var query = parentId == null
            ? dbContext.Contents.Where(c => c.ParentId == null && c.IsRootContent && !c.Deleted)
            : dbContext.Contents.Where(c => c.ParentId == parentId && !c.Deleted);

        // Get the highest sort order by ordering descending and taking the first result
        var maxSortOrder = await query
            .OrderByDescending(c => c.SortOrder)
            .Select(c => c.SortOrder)
            .FirstOrDefaultAsync(cancellationToken);

        return maxSortOrder;
    }

    private static void UpdateContentPropertyValues(IZauberDbContext dbContext, Models.Content content,
        List<ContentPropertyValue> newPropertyValues)
    {
        var deletedItems = content.PropertyData.Where(epv => newPropertyValues.All(npv => npv.Id != epv.Id)).ToList();
        foreach (var deletedItem in deletedItems)
        {
            dbContext.ContentPropertyValues.Remove(deletedItem);
        }

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

    private static async Task<PaginatedList<ContentType>> QueryContentTypesForCompositionAsync(
        IZauberDbContext dbContext, Guid compositionId, CancellationToken cancellationToken)
    {
        var query = dbContext.ContentTypes.WhereHasCompositionsUsing(compositionId);
        return await query.ToPaginatedListAsync(1, int.MaxValue, cancellationToken);
    }

    /// <summary>
    /// Stages the audit row of a change; the change's own save writes it, so a vetoed or failed change leaves none,
    /// and the save hooks still see the change's rows as added, modified or deleted (ZA-012). Nobody signed in, no row.
    /// </summary>
    private static void AddAuditIfUser(IZauberDbContext dbContext, User? user, string? name, string action)
    {
        if (user == null) return;
        dbContext.Audits.Add(new ZauberCMS.Core.Audit.Models.Audit
        {
            Description = $"{user.Name} {action} {name ?? string.Empty}"
        });
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
                handlerResult.AddMessage(
                    $"Successfully cleared orphaned RelatedContentId references for {orphanedCount} content items",
                    ResultMessageType.Success);
            }
            else
            {
                logger.LogInformation("No content items with orphaned RelatedContentId references found");
                handlerResult.Entity = 0;
                handlerResult.AddMessage("No orphaned RelatedContentId references found", ResultMessageType.Success);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred while cleaning up orphaned RelatedContentId references");
            handlerResult.AddMessage($"Failed to cleanup orphaned RelatedContentId references: {ex.Message}",
                ResultMessageType.Error);
        }

        return handlerResult;
    }

    private static IQueryable<Models.Content> ApplyNestedFilter(IQueryable<Models.Content> query,
        BaseQueryContentParameters.NestedContentFilter filter)
    {
        return filter switch
        {
            BaseQueryContentParameters.NestedContentFilter.Exclude => query.Where(c => !c.IsNestedContent),
            BaseQueryContentParameters.NestedContentFilter.Only => query.Where(c => c.IsNestedContent),
            _ => query // Include does nothing
        };
    }

    /// <summary>
    /// Validates that every nested content referenced by a BlockListEditor property of this content
    /// has a ContentTypeId in the property's configured AllowedElementTypeIds list. Logs a warning
    /// (and adds a non-fatal warning to the result) for each violation; does not block the save.
    /// </summary>
    private async Task ValidateBlockListAllowedTypesAsync(Models.Content content,
        IZauberDbContext dbContext, HandlerResult<Models.Content> handlerResult,
        CancellationToken cancellationToken)
    {
        var contentType = await dbContext.ContentTypes
            .AsNoTracking()
            .FirstOrDefaultAsync(ct => ct.Id == content.ContentTypeId, cancellationToken);

        if (contentType == null) return;

        var blockListProps = contentType.ContentProperties
            .Where(p => p.ComponentAlias == BlockListEditorComponentAlias)
            .ToList();

        if (blockListProps.Count == 0) return;

        // Build per-property allowed-type maps from settings JSON. Settings model lives in
        // ZauberCMS.Components which Core can't reference, so we parse the JSON here directly.
        var propertyAllowedTypes = new Dictionary<Guid, HashSet<Guid>>();
        var allReferencedIds = new HashSet<Guid>();

        foreach (var bp in blockListProps)
        {
            if (!string.IsNullOrWhiteSpace(bp.Settings))
            {
                try
                {
                    using var doc = JsonDocument.Parse(bp.Settings);
                    if (doc.RootElement.TryGetProperty("AllowedElementTypeIds", out var allowedProp) &&
                        allowedProp.ValueKind == JsonValueKind.Array)
                    {
                        var allowed = new HashSet<Guid>();
                        foreach (var element in allowedProp.EnumerateArray())
                        {
                            if (element.ValueKind == JsonValueKind.String &&
                                Guid.TryParse(element.GetString(), out var allowedId))
                            {
                                allowed.Add(allowedId);
                            }
                        }
                        if (allowed.Count > 0)
                        {
                            propertyAllowedTypes[bp.Id] = allowed;
                        }
                    }
                }
                catch (JsonException ex)
                {
                    logger.LogWarning(ex,
                        "Failed to parse BlockListEditor settings for property {PropertyAlias} on content type {ContentTypeAlias}",
                        bp.Alias, contentType.Alias);
                }
            }

            if (!propertyAllowedTypes.ContainsKey(bp.Id)) continue;

            var pv = content.PropertyData.FirstOrDefault(p => p.ContentTypePropertyId == bp.Id);
            if (pv == null || string.IsNullOrWhiteSpace(pv.Value)) continue;

            try
            {
                var ids = JsonSerializer.Deserialize<List<Guid>>(pv.Value);
                if (ids != null)
                {
                    foreach (var id in ids)
                    {
                        if (id != Guid.Empty) allReferencedIds.Add(id);
                    }
                }
            }
            catch (JsonException)
            {
                // Logged in the save path; skip here.
            }
        }

        if (propertyAllowedTypes.Count == 0 || allReferencedIds.Count == 0) return;

        // Batch load nested content type IDs in one query
        var nestedTypeMap = await dbContext.Contents
            .AsNoTracking()
            .Where(c => allReferencedIds.Contains(c.Id))
            .Select(c => new { c.Id, c.ContentTypeId })
            .ToDictionaryAsync(x => x.Id, x => x.ContentTypeId, cancellationToken);

        foreach (var bp in blockListProps)
        {
            if (!propertyAllowedTypes.TryGetValue(bp.Id, out var allowed)) continue;

            var pv = content.PropertyData.FirstOrDefault(p => p.ContentTypePropertyId == bp.Id);
            if (pv == null || string.IsNullOrWhiteSpace(pv.Value)) continue;

            List<Guid>? ids;
            try { ids = JsonSerializer.Deserialize<List<Guid>>(pv.Value); }
            catch (JsonException) { continue; }
            if (ids == null) continue;

            foreach (var id in ids)
            {
                if (id == Guid.Empty) continue;
                if (!nestedTypeMap.TryGetValue(id, out var nestedTypeId)) continue;
                if (allowed.Contains(nestedTypeId)) continue;

                logger.LogWarning(
                    "Block list property '{PropertyAlias}' on content {ContentId} references content {NestedId} of disallowed type {ContentTypeId}",
                    bp.Alias, content.Id, id, nestedTypeId);
                handlerResult.AddMessage(
                    $"Block list '{bp.Alias}' contains a disallowed element type",
                    ResultMessageType.Warning);
            }
        }
    }
}
