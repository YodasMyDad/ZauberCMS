using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ZauberCMS.Core.Content.Mapping;
using ZauberCMS.Core.Content.Models;
using ZauberCMS.Core.Content.Parameters;
using ZauberCMS.Core.Data;
using ZauberCMS.Core.Extensions;
using ZauberCMS.Core.Shared.Models;

namespace ZauberCMS.Core.Content.Services;

// Saving a page together with its blocks (Docs/BlockListEditor.md).
public partial class ContentService
{
    /// <summary>
    /// <see cref="SaveContentAsync"/> for a save of the item's values: the item and its blocks as one change (see
    /// <see cref="SaveContentParameters.Blocks"/>).
    /// </summary>
    private async Task<HandlerResult<Models.Content>> SaveWithBlocksAsync(SaveContentParameters parameters,
        CancellationToken cancellationToken)
    {
        using var scope = serviceScopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IZauberDbContext>();
        var user = await currentUserAccessor.GetUserAsync(cancellationToken);
        var handlerResult = new HandlerResult<Models.Content>();

        var root = parameters.Content;
        if (root == null)
        {
            handlerResult.AddMessage("Content is null", ResultMessageType.Error);
            return handlerResult;
        }

        var draftMode = parameters.SaveUnpublishedOnly;
        var drafts = new Dictionary<Guid, Models.Content>();
        foreach (var block in parameters.Blocks)
        {
            if (block.Id == root.Id)
            {
                handlerResult.AddMessage("A block can't be the content it belongs to", ResultMessageType.Error);
                return handlerResult;
            }

            drafts[block.Id] = block;
        }

        var tree = new NestedContentTree(dbContext, extensionManager, logger);
        var storedRoot = (await tree.LoadAsync([root.Id], cancellationToken)).GetValueOrDefault(root.Id);
        if (draftMode && storedRoot == null)
        {
            handlerResult.AddMessage("Save the content once before saving a draft of it", ResultMessageType.Error);
            return handlerResult;
        }

        if (storedRoot != null && !IsCurrentCopy(root, storedRoot, draftMode))
        {
            handlerResult.AddMessage(
                $"{storedRoot.Name} was changed by someone else since it was opened. Reload it and make the changes again.",
                ResultMessageType.Error);
            return handlerResult;
        }

        if (storedRoot != null)
        {
            // Which draft a save replaces is what is stored, not what the caller's copy says
            root.UnpublishedContentId = storedRoot.UnpublishedContentId;
        }

        // What the stored item reaches now: through its live values, and through drafts (its own, and those its
        // blocks still have, even when the item's own draft is gone)
        var liveBefore = storedRoot == null
            ? new NestedContentTree.Walk()
            : await tree.WalkAsync(storedRoot, null, useStoredDrafts: false, cancellationToken);
        var draftBefore = storedRoot == null
            ? liveBefore
            : await tree.WalkAsync(NestedContentTree.WithStoredDraft(storedRoot), null, useStoredDrafts: true,
                cancellationToken);
        var before = new HashSet<Guid>(liveBefore.Ids);
        before.UnionWith(draftBefore.Ids);

        // What the new values reach. Blocks the caller sent that are not reached are ignored.
        var newTree = await tree.WalkAsync(root, drafts, useStoredDrafts: true, cancellationToken);
        if (newTree.Truncated)
        {
            handlerResult.AddMessage(
                $"The content has more blocks or deeper nesting than can be saved ({NestedContentTree.MaxBlocks} blocks, {NestedContentTree.MaxDepth} levels)",
                ResultMessageType.Error);
            return handlerResult;
        }

        foreach (var node in newTree.Nodes.Where(n => n.Stored != null))
        {
            if (!node.Stored!.IsNestedContent)
            {
                handlerResult.AddMessage($"'{node.Stored.Name}' is not a block", ResultMessageType.Error);
                return handlerResult;
            }

            if (!before.Contains(node.Id))
            {
                // A block is owned by one item; pointing a second one at it would let either delete it
                handlerResult.AddMessage($"The block '{node.Stored.Name}' belongs to other content",
                    ResultMessageType.Error);
                return handlerResult;
            }
        }

        // A draft leaves the live tree as it is, so the blocks it reaches stay too
        var after = new HashSet<Guid>(newTree.Ids);
        if (draftMode)
        {
            after.UnionWith(liveBefore.Ids);
        }

        await WarnAboutDisallowedBlockTypesAsync(tree, root, newTree, handlerResult, cancellationToken);

        // The item itself
        Models.Content rootEntity;
        var isUpdate = storedRoot != null;
        var hookEntities = new List<Models.Content>();
        if (draftMode)
        {
            await StageDraftAsync(dbContext, root, storedRoot!.UnpublishedContentId, cancellationToken);
            rootEntity = root;
        }
        else
        {
            var staged = await StageContentAsync(dbContext, user, new SaveContentParameters
            {
                Content = root, Roles = parameters.Roles, UpdateContentRoles = parameters.UpdateContentRoles
            }, handlerResult, validateBlockTypes: false, cancellationToken);
            if (staged == null)
            {
                return handlerResult;
            }

            rootEntity = staged.Content;
            isUpdate = staged.IsUpdate;
            hookEntities.Add(rootEntity);
        }

        // Its blocks, parents first
        foreach (var node in newTree.Nodes)
        {
            if (node.FromCaller)
            {
                var block = PrepareBlock(node);
                if (draftMode && node.Stored != null)
                {
                    await StageDraftAsync(dbContext, block, node.Stored.UnpublishedContentId, cancellationToken);
                    continue;
                }

                // A new block is stored as it is even in a draft: only the draft reaches it, so the live site does not
                var staged = await StageContentAsync(dbContext, user, new SaveContentParameters { Content = block },
                    handlerResult, validateBlockTypes: false, cancellationToken);
                if (staged == null)
                {
                    return handlerResult;
                }

                hookEntities.Add(staged.Content);
            }
            else if (!draftMode && node.Stored?.UnpublishedContent != null)
            {
                // A draft from an earlier "Save only" that this edit did not open: it is published with the item
                hookEntities.Add(await StagePublishBlockDraftAsync(dbContext, node.Stored, user?.Id, cancellationToken));
            }
        }

        // Blocks reached before and not any more, with everything below them
        var removed = await tree.StageDeleteAsync(before.Where(id => !after.Contains(id)).ToList(), cancellationToken);
        if (removed.Count > 0)
        {
            logger.LogInformation("Saving content {ContentId} deleted {Count} block(s) it no longer uses",
                root.Id, removed.Count);
        }

        // Services trust their caller (Roadmap D27): a save with nobody signed in (a job, an import) records no user
        if (!draftMode && user != null)
        {
            dbContext.Audits.Add(new Audit.Models.Audit
            {
                Description = $"{user.Name} {(isUpdate ? "Updated" : "Created")} {rootEntity.Name ?? nameof(Models.Content)}"
            });
        }

        HandlerResult<Models.Content> saveResult;
        await using (var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken))
        {
            try
            {
                saveResult = await dbContext.SaveChangesAndLog([.. hookEntities, .. removed], rootEntity,
                    handlerResult, cacheService, extensionManager, cancellationToken);
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
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        }

        if (saveResult.Success)
        {
            // After the blocks are written, so the version holds the item and its blocks as saved (ZA-008)
            var published = !draftMode && rootEntity.Published;
            await CreateVersionAfterSaveAsync(scope, rootEntity,
                published ? ContentVersionStatus.Published : ContentVersionStatus.Draft,
                draftMode
                    ? "Draft saved - unpublished changes"
                    : published
                        ? (isUpdate ? "Content updated and published" : "Content created and published")
                        : (isUpdate ? "Content updated" : "Content created"),
                user?.Id, rootEntity.Id, cancellationToken);
        }

        return saveResult;
    }

    /// <summary>
    /// Whether the caller edited the item as it is stored. A draft does not change the item's <c>DateUpdated</c>, so
    /// the stored draft is compared too: a draft saved by someone else after the caller opened the item (or one they
    /// cleared or published) would otherwise be overwritten, and the blocks only it had deleted. A "Save only" also
    /// checks <c>DateUpdated</c> itself, which the publish path checks when it saves.
    /// </summary>
    private static bool IsCurrentCopy(Models.Content root, Models.Content stored, bool draftMode)
    {
        if (root.UnpublishedContentId != stored.UnpublishedContentId) return false;
        if (stored.UnpublishedContent != null && root.UnpublishedContent != null &&
            root.UnpublishedContent.DateUpdated != stored.UnpublishedContent.DateUpdated)
        {
            return false;
        }

        return !draftMode || root.DateUpdated == default || root.DateUpdated == stored.DateUpdated;
    }

    /// <summary>A copy of the caller's block, made a block of the item it sits under.</summary>
    private static Models.Content PrepareBlock(NestedContentTree.Node node)
    {
        var block = node.Values.MapToNew();
        block.IsNestedContent = true;
        block.RelatedContentId = node.ParentId;
        // The stored link, not whatever the caller's copy holds, decides which draft a save replaces
        block.UnpublishedContentId = node.Stored?.UnpublishedContentId;
        if (node.Stored != null)
        {
            // Where a block sits is decided by the values that list it, not by the block
            block.ParentId = node.Stored.ParentId;
            block.Path = node.Stored.Path.ToList();
            block.DateCreated = node.Stored.DateCreated;
        }

        block.PropertyData = node.Values.PropertyData.Select(p => new ContentPropertyValue
        {
            Id = p.Id,
            ContentId = block.Id,
            Alias = p.Alias,
            ContentTypePropertyId = p.ContentTypePropertyId,
            Value = p.Value,
            DateCreated = p.DateCreated,
            DateUpdated = p.DateUpdated
        }).ToList();
        return block;
    }

    /// <summary>Stages the publish of a block's stored draft onto its row and removes the draft.</summary>
    private static async Task<Models.Content> StagePublishBlockDraftAsync(IZauberDbContext dbContext,
        Models.Content stored, Guid? userId, CancellationToken cancellationToken)
    {
        var draft = stored.UnpublishedContent!.JsonContent;
        var row = await dbContext.Contents.AsTracking().Include(x => x.PropertyData)
            .FirstAsync(x => x.Id == stored.Id, cancellationToken);
        draft.MapDraftTo(row);
        row.Published = draft.Published;
        UpdateContentPropertyValues(dbContext, row, draft.PropertyData.Select(p => new ContentPropertyValue
        {
            Id = p.Id,
            ContentId = row.Id,
            Alias = p.Alias,
            ContentTypePropertyId = p.ContentTypePropertyId,
            Value = p.Value,
            DateCreated = p.DateCreated,
            DateUpdated = p.DateUpdated
        }).ToList());

        var storedDraft = await dbContext.UnpublishedContent.AsTracking()
            .FirstOrDefaultAsync(x => x.Id == stored.UnpublishedContentId, cancellationToken);
        if (storedDraft != null)
        {
            dbContext.UnpublishedContent.Remove(storedDraft);
        }

        row.UnpublishedContentId = null;
        row.DateUpdated = DateTime.UtcNow;
        row.LastUpdatedById = userId;
        return row;
    }

    /// <summary>
    /// Warns (does not refuse) when a block list holds a block of a type its settings do not allow. The editor only
    /// offers allowed types; this catches old data and crafted saves.
    /// </summary>
    private async Task WarnAboutDisallowedBlockTypesAsync(NestedContentTree tree, Models.Content root,
        NestedContentTree.Walk walk, HandlerResult<Models.Content> handlerResult, CancellationToken cancellationToken)
    {
        var typeById = walk.Nodes.ToDictionary(n => n.Id, n => n.Values.ContentTypeId);
        foreach (var parent in walk.Nodes.Select(n => n.Values).Prepend(root))
        {
            foreach (var (_, property, ids) in await tree.GetReferencesAsync(parent, cancellationToken))
            {
                var allowed = ParseAllowedElementTypeIds(property.Property.Settings);
                if (allowed.Count == 0) continue;
                foreach (var id in ids)
                {
                    if (!typeById.TryGetValue(id, out var typeId) || allowed.Contains(typeId)) continue;
                    logger.LogWarning(
                        "Block list property '{PropertyAlias}' on content {ContentId} holds block {BlockId} of disallowed type {ContentTypeId}",
                        property.Property.Alias, parent.Id, id, typeId);
                    handlerResult.AddMessage($"Block list '{property.Property.Alias}' contains a disallowed element type",
                        ResultMessageType.Warning);
                }
            }
        }
    }

    /// <summary>The Block List editor's <c>AllowedElementTypeIds</c> setting (empty when not set).</summary>
    private static HashSet<Guid> ParseAllowedElementTypeIds(string? settings)
    {
        var allowed = new HashSet<Guid>();
        if (string.IsNullOrWhiteSpace(settings)) return allowed;
        try
        {
            using var doc = JsonDocument.Parse(settings);
            if (doc.RootElement.ValueKind == JsonValueKind.Object &&
                doc.RootElement.TryGetProperty("AllowedElementTypeIds", out var ids) &&
                ids.ValueKind == JsonValueKind.Array)
            {
                foreach (var element in ids.EnumerateArray())
                {
                    if (element.ValueKind == JsonValueKind.String && Guid.TryParse(element.GetString(), out var id))
                    {
                        allowed.Add(id);
                    }
                }
            }
        }
        catch (JsonException)
        {
            // Settings in another shape: nothing to check against
        }

        return allowed;
    }
}
