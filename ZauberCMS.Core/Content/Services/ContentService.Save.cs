using System.Linq.Dynamic.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ZauberCMS.Core.Content.Interfaces;
using ZauberCMS.Core.Content.Models;
using ZauberCMS.Core.Content.Parameters;
using ZauberCMS.Core.Content.Mapping;
using ZauberCMS.Core.Data;
using ZauberCMS.Core.Extensions;
using ZauberCMS.Core.Plugins;
using ZauberCMS.Core.Settings;
using ZauberCMS.Core.Shared.Models;
using ZauberCMS.Core.Shared.Services;
using ZauberCMS.Core.Membership.Interfaces;
using ZauberCMS.Core.Membership.Models;
using System.Text.Json;

namespace ZauberCMS.Core.Content.Services;

// Saving a page: staging, drafts, versions, URLs, roles and property values (the page with its blocks: ContentService.Tree.cs).
public partial class ContentService
{
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
            parameters.Content.Url = null;
        }
        else if (parameters.Content.Url.IsNullOrWhiteSpace())
        {
            var baseSlug = new SlugHelper().GenerateSlug(parameters.Content.Name);
            parameters.Content.Url = await GenerateUniqueUrlAsync(dbContext, baseSlug, cancellationToken, parameters.Content.Id);
            urlGenerated = true;
        }
        else
        {
            // The front-end lookup normalises the request slug the same way (ContentUrl.Normalize), so a
            // stored URL with mixed case or slashes around it would never match (ZA-066).
            parameters.Content.Url = ContentUrl.Normalize(parameters.Content.Url);
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
        var storedUrl = content?.Url;

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
        if (!content.IsNestedContent && !content.Deleted && !content.Url.IsNullOrWhiteSpace())
        {
            var url = content.Url!;
            var urlChanged = !isUpdate || isRestore || !string.Equals(url, storedUrl, StringComparison.Ordinal);
            if (urlChanged && await IsUrlTakenAsync(dbContext, url, content.Id, cancellationToken))
            {
                if (urlGenerated || isRestore)
                {
                    content.Url = await GenerateUniqueUrlAsync(dbContext, url, cancellationToken, content.Id);
                    if (isRestore)
                    {
                        await KeepDraftUrlAsync(dbContext, content, cancellationToken);
                        handlerResult.AddMessage(
                            $"The URL '{url}' is now used by another page, so the restored page has the URL '{content.Url}'",
                            ResultMessageType.Warning);
                    }
                }
                else
                {
                    handlerResult.AddMessage($"The URL '{url}' is already used by another page", ResultMessageType.Error);
                    return null;
                }
            }

            reservedUrls.Add(content.Url!);
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
                    var descendantUrl = descendant.Url;
                    if (!descendant.IsNestedContent && !descendantUrl.IsNullOrWhiteSpace())
                    {
                        if (reservedUrls.Contains(descendantUrl!) || await IsUrlTakenAsync(dbContext, descendantUrl!, descendant.Id, cancellationToken))
                        {
                            descendant.Url = await GenerateUniqueUrlAsync(dbContext, descendantUrl!, cancellationToken, descendant.Id, reservedUrls);
                            await KeepDraftUrlAsync(dbContext, descendant, cancellationToken);
                            handlerResult.AddMessage(
                                $"The URL '{descendantUrl}' is now used by another page, so the restored page '{descendant.Name}' has the URL '{descendant.Url}'",
                                ResultMessageType.Warning);
                        }

                        reservedUrls.Add(descendant.Url!);
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
        return await dbContext.Contents.AsNoTracking()
            .AnyAsync(c => c.Url == url && !c.Deleted && !c.IsNestedContent && c.Id != id, cancellationToken);
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
        var urls = await dbContext.Contents.AsNoTracking()
            .Where(c => (c.Url == baseSlug || c.Url!.StartsWith(numbered)) && !c.Deleted && !c.IsNestedContent &&
                        c.Id != id)
            .Select(c => c.Url!)
            .ToListAsync(cancellationToken);
        // Stored URLs are lower case (ContentUrl.Normalize), so letter case only matters for rows from before that;
        // ignoring it here never hands out a URL that differs from another page's by case alone
        return urls.ToHashSet(StringComparer.OrdinalIgnoreCase);
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
        draft.JsonContent.Url = item.Url;
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
