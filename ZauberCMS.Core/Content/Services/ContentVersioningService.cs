using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ZauberCMS.Core.Content.Interfaces;
using ZauberCMS.Core.Content.Mapping;
using ZauberCMS.Core.Content.Models;
using ZauberCMS.Core.Content.Parameters;
using ZauberCMS.Core.Data;
using ZauberCMS.Core.Extensions;
using ZauberCMS.Core.Membership.Models;
using ZauberCMS.Core.Plugins;
using ZauberCMS.Core.Shared.Models;
using ZauberCMS.Core.Shared.Services;
using ZauberCMS.Core.Tags.Services;

namespace ZauberCMS.Core.Content.Services;

public class ContentVersioningService(
    IServiceScopeFactory serviceScopeFactory,
    ICacheService cacheService,
    ExtensionManager extensionManager,
    ILogger<ContentVersioningService> logger)
    : IContentVersioningService
{
    private readonly IServiceScopeFactory _serviceScopeFactory = serviceScopeFactory;

    private const int MaxSnapshotsPerVersion = NestedContentTree.MaxBlocks;

    /// <summary>
    /// Creates a new version of content
    /// </summary>
    public async Task<HandlerResult<ContentVersion>> CreateVersionAsync(CreateContentVersionParameters parameters, CancellationToken cancellationToken = default)
    {
        using var scope = _serviceScopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IZauberDbContext>();
        var handlerResult = new HandlerResult<ContentVersion>();

        if (parameters.Content == null)
        {
            handlerResult.AddMessage("Content is null", ResultMessageType.Error);
            return handlerResult;
        }

        // One transaction (disposed without a commit, it rolls back), the page row locked first: two versions of
        // one page at the same moment run one after the other, each reading the page, numbering itself and switching
        // the old flag after the other has committed (ZA-146)
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        await LockContentRowAsync(dbContext, parameters.Content.Id, cancellationToken);

        var content = await dbContext.Contents
            .Include(c => c.PropertyData)
            .Include(c => c.ContentType)
            .FirstOrDefaultAsync(c => c.Id == parameters.Content.Id, cancellationToken);

        if (content == null)
        {
            handlerResult.AddMessage("Content not found", ResultMessageType.Error);
            return handlerResult;
        }

        // A draft version records the draft: its name and values are in the stored draft, while the live row
        // still has the published ones (ZA-074). Where the page sits in the tree comes from the live row.
        var snapshotSource = content;
        if (parameters.Status == ContentVersionStatus.Draft && content.UnpublishedContentId != null)
        {
            var draft = await dbContext.UnpublishedContent.AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == content.UnpublishedContentId, cancellationToken);
            if (draft != null)
            {
                snapshotSource = draft.JsonContent.MapDraftTo(content.MapToNew());
                snapshotSource.PropertyData = draft.JsonContent.PropertyData;
            }
        }

        var version = new ContentVersion
        {
            ContentId = content.Id,
            // Numbered below, under the lock
            VersionName = parameters.VersionName,
            Status = parameters.Status,
            Comments = parameters.Comments,
            CreatedById = parameters.CreatedByUserId,
            IsCurrentPublished = parameters.Status == ContentVersionStatus.Published,
            IsLatestDraft = parameters.Status == ContentVersionStatus.Draft,
            IsAutoSave = parameters.IsAutoSave,
            ParentVersionId = parameters.ParentVersionId,
            Tags = parameters.Tags ?? [],
            Snapshot = CreateContentSnapshot(snapshotSource),
            PropertySnapshots = snapshotSource.PropertyData?.Select(p => new ContentPropertySnapshot
            {
                ContentTypePropertyId = p.ContentTypePropertyId,
                Alias = p.Alias,
                Value = p.Value,
                DateCreated = p.DateCreated ?? DateTime.UtcNow,
                DateUpdated = p.DateUpdated ?? DateTime.UtcNow
            }).ToList() ?? [],
            BlockListSnapshots = await CreateBlockListSnapshotsAsync(dbContext, snapshotSource,
                parameters.Status == ContentVersionStatus.Draft, cancellationToken),
            ContentSize = CalculateContentSize(snapshotSource)
        };

        // Version created successfully

        if (parameters.Status == ContentVersionStatus.Published)
        {
            version.DatePublished = DateTime.UtcNow;
        }
        else
        {
            version.DatePublished = null;
        }

        // The version records the content as it is; it is not written back to it (that only touched DateUpdated,
        // the concurrency token, so the next save of the copy the editor holds was refused)

        // A page has one current published version and one latest draft (unique indexes). The old flag is switched
        // off and saved first, in the same transaction: in one save the order of the two writes is not fixed.
        version.VersionNumber = (await dbContext.ContentVersions
            .Where(v => v.ContentId == content.Id)
            .MaxAsync(v => (int?)v.VersionNumber, cancellationToken) ?? 0) + 1;

        var cleared = await ClearFlagsAsync(dbContext, content.Id, version.IsCurrentPublished,
            version.IsLatestDraft, null, cancellationToken);
        if (!cleared.Success)
        {
            handlerResult.Messages.AddRange(cleared.Messages);
            return handlerResult;
        }

        dbContext.ContentVersions.Add(version);
        var result = await dbContext.SaveChangesAndLog(version, handlerResult, cacheService, extensionManager,
            cancellationToken);
        if (result.Success)
        {
            await transaction.CommitAsync(cancellationToken);
        }
        else
        {
            await transaction.RollbackAsync(CancellationToken.None);
        }

        // Invalidate content cache. Cache keys are formatted "{TypeName}-{hash}" (see
        // CacheExtensions.ToCacheKey), so the prefix must use a hyphen and the entity name —
        // "Content_{id}" with an underscore would never match anything.
        cacheService.ClearCachedItemsWithPrefix(nameof(Models.Content));

        return result;
    }

    /// <summary>
    /// Restores a version: the content gets the version's name, navigation settings, property values and blocks.
    /// Where the content sits in the tree, its language and whether it is published stay as they are now (ZA-075).
    /// A pending draft is discarded, because it was based on the values this replaces.
    /// </summary>
    public async Task<HandlerResult<ContentVersion>> PublishVersionAsync(PublishContentVersionParameters parameters, CancellationToken cancellationToken = default)
    {
        using var scope = _serviceScopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IZauberDbContext>();
        var handlerResult = new HandlerResult<ContentVersion>();

        var version = await dbContext.ContentVersions
            .FirstOrDefaultAsync(v => v.Id == parameters.VersionId, cancellationToken);

        if (version == null)
        {
            handlerResult.AddMessage("Version not found", ResultMessageType.Error);
            return handlerResult;
        }

        var content = await dbContext.Contents
            .Include(c => c.PropertyData)
            .FirstOrDefaultAsync(c => c.Id == version.ContentId, cancellationToken);

        if (content == null)
        {
            handlerResult.AddMessage("Content not found", ResultMessageType.Error);
            return handlerResult;
        }

        // One transaction from here (disposed without a commit, it rolls back). The page row is locked first, so a
        // version saved for the page at the same moment waits (ZA-146); the old published version's flag is switched
        // off and saved before anything else changes, so the save hooks see it and the unique index holds.
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        await LockContentRowAsync(dbContext, content.Id, cancellationToken);
        if (content.Published || version.IsCurrentPublished)
        {
            var cleared = await ClearFlagsAsync(dbContext, content.Id, currentPublished: true, latestDraft: false,
                keepVersionId: version.Id, cancellationToken);
            if (!cleared.Success)
            {
                handlerResult.Messages.AddRange(cleared.Messages);
                return handlerResult;
            }
        }

        // The blocks the content reaches now, live and through drafts, before the restore changes its values
        var tree = new NestedContentTree(dbContext, extensionManager, logger);
        var storedContent = (await tree.LoadAsync([content.Id], cancellationToken))[content.Id];
        var blocksBefore = (await tree.WalkAsync(storedContent, null, useStoredDrafts: false, cancellationToken)).Ids;
        if (storedContent.UnpublishedContent != null)
        {
            blocksBefore.UnionWith((await tree.WalkAsync(NestedContentTree.WithStoredDraft(storedContent), null,
                useStoredDrafts: true, cancellationToken)).Ids);
        }

        if (content.Published)
        {
            // The restored values are what is live now (the old flag was switched off above)
            version.Status = ContentVersionStatus.Published;
            version.IsCurrentPublished = true;
            version.DatePublished = DateTime.UtcNow;
            version.CreatedById = parameters.PublishedByUserId; // Update the publisher
        }

        RestoreVersionToContent(dbContext, version, content);
        content.DateUpdated = DateTime.UtcNow;
        if (parameters.PublishedByUserId != null)
        {
            content.LastUpdatedById = parameters.PublishedByUserId;
        }

        if (content.UnpublishedContentId != null)
        {
            var draft = await dbContext.UnpublishedContent
                .FirstOrDefaultAsync(u => u.Id == content.UnpublishedContentId, cancellationToken);
            content.UnpublishedContentId = null;
            if (draft != null)
            {
                dbContext.UnpublishedContent.Remove(draft);
            }
        }

        // Restore block list content from snapshots. The version's blocks are what the content holds now: their
        // pending drafts go (they were edits of what this replaces), and blocks only the replaced values used are
        // deleted.
        await RestoreBlockListContentAsync(dbContext, version, cancellationToken);
        var restoredBlockIds = version.BlockListSnapshots.Where(s => s.ContentSnapshot.IsNestedContent)
            .Select(s => s.ContentId).ToHashSet();

        // What the restored values reach: the version's blocks as restored, and blocks it has no snapshot of (a
        // version made before blocks were saved with their page, or one cut off at the limit) as they are stored.
        // Only blocks reached before and not now are deleted.
        var restoredBlocks = dbContext.Contents.Local.Where(c => restoredBlockIds.Contains(c.Id))
            .ToDictionary(c => c.Id);
        var stillUsed = (await tree.WalkAsync(content, restoredBlocks, useStoredDrafts: false, cancellationToken)).Ids;
        stillUsed.UnionWith(restoredBlockIds);
        await tree.StageDiscardDraftsAsync(stillUsed, cancellationToken);
        await tree.StageDeleteAsync(blocksBefore.Where(id => !stillUsed.Contains(id)).ToList(), cancellationToken);

        var userName = parameters.PublishedByUserId == null
            ? null
            : await dbContext.Users.AsNoTracking().Where(u => u.Id == parameters.PublishedByUserId)
                .Select(u => u.UserName).FirstOrDefaultAsync(cancellationToken);
        dbContext.Audits.Add(new Audit.Models.Audit
        {
            Description = $"{userName ?? "Unknown"} Restored {content.Name} to version {version.VersionNumber}"
        });

        // The restored values set the tag links, as a publish does
        await ContentTagLinks.StageAsync(dbContext, extensionManager, [content, .. restoredBlocks.Values],
            cancellationToken);

        // Through SaveChangesAndLog so the save hooks run for every row the restore writes (the page, its blocks,
        // their drafts, the version, the audit row) and the content cache is cleared
        var contentResult = await dbContext.SaveChangesAndLog(content, new HandlerResult<Models.Content>(),
            cacheService, extensionManager, cancellationToken);
        if (contentResult.Success)
        {
            await transaction.CommitAsync(cancellationToken);
        }
        else
        {
            await transaction.RollbackAsync(CancellationToken.None);
        }

        handlerResult.Success = contentResult.Success;
        handlerResult.Messages.AddRange(contentResult.Messages);
        if (contentResult.Success)
        {
            handlerResult.Entity = version;
        }

        // Clear content cache. See CreateVersionAsync for prefix-format reasoning.
        cacheService.ClearCachedItemsWithPrefix(nameof(Models.Content));

        return handlerResult;
    }

    /// <summary>
    /// Gets versions for a content item
    /// </summary>
    public async Task<PaginatedList<ContentVersion>> GetContentVersionsAsync(QueryContentVersionsParameters parameters, CancellationToken cancellationToken = default)
    {
        using var scope = _serviceScopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IZauberDbContext>();

        var query = dbContext.ContentVersions.AsNoTracking()
            .Where(v => v.ContentId == parameters.ContentId);

        // Apply filters
        if (parameters.Status.HasValue)
        {
            query = query.Where(v => v.Status == parameters.Status.Value);
        }

        if (parameters.CreatedById.HasValue)
        {
            query = query.Where(v => v.CreatedById == parameters.CreatedById.Value);
        }

        if (!string.IsNullOrWhiteSpace(parameters.VersionName))
        {
            query = query.Where(v => v.VersionName != null && v.VersionName.Contains(parameters.VersionName));
        }

        if (parameters.DateFrom.HasValue)
        {
            query = query.Where(v => v.DateCreated >= parameters.DateFrom.Value);
        }

        if (parameters.DateTo.HasValue)
        {
            query = query.Where(v => v.DateCreated <= parameters.DateTo.Value);
        }

        if (parameters.IncludeAutoSaves == false)
        {
            query = query.Where(v => !v.IsAutoSave);
        }

        // Apply ordering
        query = parameters.OrderBy switch
        {
            ContentVersionOrderBy.VersionNumber => query.OrderByDescending(v => v.VersionNumber),
            ContentVersionOrderBy.DateCreated => query.OrderByDescending(v => v.DateCreated),
            ContentVersionOrderBy.DatePublished => query.OrderByDescending(v => v.DatePublished),
            _ => query.OrderByDescending(v => v.VersionNumber)
        };

        var totalCount = await query.CountAsync(cancellationToken);
        var page = query
            .Skip((parameters.PageIndex - 1) * parameters.AmountPerPage)
            .Take(parameters.AmountPerPage);
        // Not tracked, and CreatedBy without the rest of the user row (password hash, security stamp); the
        // snapshots only when asked for (ZA-069)
        var items = parameters.IncludeSnapshots
            ? await page.Select(v => new ContentVersion
            {
                Id = v.Id, ContentId = v.ContentId, VersionNumber = v.VersionNumber, VersionName = v.VersionName,
                Status = v.Status, CreatedById = v.CreatedById, DateCreated = v.DateCreated,
                DatePublished = v.DatePublished, Comments = v.Comments, IsCurrentPublished = v.IsCurrentPublished,
                IsLatestDraft = v.IsLatestDraft, ParentVersionId = v.ParentVersionId, Tags = v.Tags,
                IsAutoSave = v.IsAutoSave, ContentSize = v.ContentSize,
                Snapshot = v.Snapshot, PropertySnapshots = v.PropertySnapshots,
                BlockListSnapshots = v.BlockListSnapshots,
                CreatedBy = v.CreatedBy == null
                    ? null
                    : new User { Id = v.CreatedBy.Id, UserName = v.CreatedBy.UserName, Email = v.CreatedBy.Email }
            }).ToListAsync(cancellationToken)
            : await page.Select(v => new ContentVersion
            {
                Id = v.Id, ContentId = v.ContentId, VersionNumber = v.VersionNumber, VersionName = v.VersionName,
                Status = v.Status, CreatedById = v.CreatedById, DateCreated = v.DateCreated,
                DatePublished = v.DatePublished, Comments = v.Comments, IsCurrentPublished = v.IsCurrentPublished,
                IsLatestDraft = v.IsLatestDraft, ParentVersionId = v.ParentVersionId, Tags = v.Tags,
                IsAutoSave = v.IsAutoSave, ContentSize = v.ContentSize,
                CreatedBy = v.CreatedBy == null
                    ? null
                    : new User { Id = v.CreatedBy.Id, UserName = v.CreatedBy.UserName, Email = v.CreatedBy.Email }
            }).ToListAsync(cancellationToken);

        return new PaginatedList<ContentVersion>
        {
            Items = items,
            TotalItems = totalCount,
            PageIndex = parameters.PageIndex,
            TotalPages = (int)Math.Ceiling(totalCount / (double)parameters.AmountPerPage)
        };
    }

    /// <summary>
    /// Gets a specific version
    /// </summary>
    public async Task<ContentVersion?> GetVersionAsync(GetContentVersionParameters parameters, CancellationToken cancellationToken = default)
    {
        using var scope = _serviceScopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IZauberDbContext>();

        return await dbContext.ContentVersions
            .Include(v => v.CreatedBy)
            .FirstOrDefaultAsync(v => v.Id == parameters.VersionId, cancellationToken);
    }

    /// <summary>
    /// Deletes a version (with safety checks)
    /// </summary>
    public async Task<HandlerResult<bool>> DeleteVersionAsync(DeleteContentVersionParameters parameters, CancellationToken cancellationToken = default)
    {
        using var scope = _serviceScopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IZauberDbContext>();
        var handlerResult = new HandlerResult<bool>();

        var version = await dbContext.ContentVersions
            .FirstOrDefaultAsync(v => v.Id == parameters.VersionId, cancellationToken);

        if (version == null)
        {
            handlerResult.AddMessage("Version not found", ResultMessageType.Error);
            return handlerResult;
        }

        // Prevent deletion of current published version
        if (version.IsCurrentPublished)
        {
            handlerResult.AddMessage("Cannot delete the currently published version", ResultMessageType.Error);
            return handlerResult;
        }

        // Prevent deletion of latest draft if it's the only draft
        if (version.IsLatestDraft)
        {
            var otherDrafts = await dbContext.ContentVersions
                .CountAsync(v => v.ContentId == version.ContentId && v.Status == ContentVersionStatus.Draft && v.Id != version.Id, cancellationToken);

            if (otherDrafts == 0)
            {
                handlerResult.AddMessage("Cannot delete the only draft version", ResultMessageType.Error);
                return handlerResult;
            }
        }

        dbContext.ContentVersions.Remove(version);
        var deleted = await dbContext.SaveChangesAndLog(version, new HandlerResult<ContentVersion>(), cacheService,
            extensionManager, cancellationToken);
        handlerResult.Success = deleted.Success;
        handlerResult.Messages.AddRange(deleted.Messages);
        handlerResult.Entity = deleted.Success;

        return handlerResult;
    }

    /// <inheritdoc />
    public async Task<HandlerResult<int>> PruneVersionsAsync(PruneContentVersionsParameters parameters,
        CancellationToken cancellationToken = default)
    {
        var result = new HandlerResult<int> { Success = true };
        if (parameters.VersionsToKeep <= 0)
        {
            return result;
        }

        using var scope = _serviceScopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IZauberDbContext>();
        var keep = parameters.VersionsToKeep;

        // Only the pages over the limit, one page at a time: a version row can be large (snapshots)
        var contentIds = await dbContext.ContentVersions.AsNoTracking()
            .GroupBy(v => v.ContentId)
            .Where(g => g.Count() > keep)
            .Select(g => g.Key)
            .ToListAsync(cancellationToken);

        foreach (var contentId in contentIds)
        {
            var oldIds = await dbContext.ContentVersions.AsNoTracking()
                .Where(v => v.ContentId == contentId)
                .OrderByDescending(v => v.VersionNumber)
                .Skip(keep)
                .Where(v => !v.IsCurrentPublished && !v.IsLatestDraft)
                .Select(v => v.Id)
                .ToListAsync(cancellationToken);

            foreach (var chunk in oldIds.Chunk(500))
            {
                // A kept version may name a removed one as its parent (the foreign key has no cascade)
                await dbContext.ContentVersions
                    .Where(v => v.ParentVersionId != null && chunk.Contains(v.ParentVersionId.Value))
                    .ExecuteUpdateAsync(s => s.SetProperty(v => v.ParentVersionId, (Guid?)null), cancellationToken);
                result.Entity += await dbContext.ContentVersions
                    .Where(v => chunk.Contains(v.Id))
                    .ExecuteDeleteAsync(cancellationToken);
            }
        }

        if (result.Entity > 0)
        {
            logger.LogInformation("Removed {Count} content versions beyond the newest {Keep} of each page",
                result.Entity, keep);
        }

        return result;
    }

    /// <summary>
    /// Compares two versions and returns the differences
    /// </summary>
    public async Task<ContentVersionComparison> CompareVersionsAsync(CompareContentVersionsParameters parameters, CancellationToken cancellationToken = default)
    {
        var version1 = await GetVersionAsync(new GetContentVersionParameters { VersionId = parameters.VersionId1 }, cancellationToken);
        var version2 = await GetVersionAsync(new GetContentVersionParameters { VersionId = parameters.VersionId2 }, cancellationToken);

        if (version1 == null || version2 == null)
        {
            throw new ArgumentException("One or both versions not found");
        }

        return new ContentVersionComparison
        {
            Version1 = version1,
            Version2 = version2,
            Differences = CompareSnapshots(version1.Snapshot, version2.Snapshot),
            PropertyDifferences = ComparePropertySnapshots(version1.PropertySnapshots, version2.PropertySnapshots),
            BlockListDifferences = CompareBlockListSnapshots(version1, version2)
        };
    }

    #region Private Methods

    /// <summary>
    /// Locks the page's row until the caller's transaction ends (an update that changes nothing), so two version
    /// writes for one page run one after the other on every database provider (ZA-146).
    /// </summary>
    private static Task LockContentRowAsync(IZauberDbContext dbContext, Guid contentId,
        CancellationToken cancellationToken) =>
        dbContext.Contents.Where(c => c.Id == contentId)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.DateUpdated, c => c.DateUpdated), cancellationToken);

    /// <summary>
    /// Switches the page's current-published and/or latest-draft flag off on its other versions and saves that on
    /// its own (inside the caller's transaction), through the save hooks, before the new flag is saved: a page has one
    /// of each (unique indexes), and in one save the order of the two writes is not fixed.
    /// </summary>
    private async Task<HandlerResult<ContentVersion>> ClearFlagsAsync(IZauberDbContext dbContext, Guid contentId,
        bool currentPublished, bool latestDraft, Guid? keepVersionId, CancellationToken cancellationToken)
    {
        var result = new HandlerResult<ContentVersion> { Success = true };
        if (!currentPublished && !latestDraft) return result;

        var flagged = await dbContext.ContentVersions.AsTracking()
            .Where(v => v.ContentId == contentId && v.Id != keepVersionId &&
                        ((currentPublished && v.IsCurrentPublished) || (latestDraft && v.IsLatestDraft)))
            .ToListAsync(cancellationToken);
        if (flagged.Count == 0) return result;

        foreach (var previous in flagged)
        {
            if (currentPublished) previous.IsCurrentPublished = false;
            if (latestDraft) previous.IsLatestDraft = false;
        }

        return await dbContext.SaveChangesAndLog(flagged, null, result, cacheService, extensionManager,
            cancellationToken);
    }

    // Mutates entities tracked by the supplied dbContext so the caller's save persists them.
    // Only what an editor edits comes back: name, navigation settings and property values. Tree position
    // (parent, path, sort order), language, content type, URL and publish state stay as they are (ZA-075).
    private static void RestoreVersionToContent(IZauberDbContext dbContext, ContentVersion version, Models.Content content)
    {
        content.Name = version.Snapshot.Name;
        content.HideFromNavigation = version.Snapshot.HideFromNavigation;
        content.InternalRedirectId = version.Snapshot.InternalRedirectId;

        // Update property values
        foreach (var propertySnapshot in version.PropertySnapshots)
        {
            var existingProperty = content.PropertyData.FirstOrDefault(p => p.ContentTypePropertyId == propertySnapshot.ContentTypePropertyId);
            if (existingProperty != null)
            {
                existingProperty.Value = propertySnapshot.Value;
                existingProperty.DateUpdated = DateTime.UtcNow;
            }
            else
            {
                content.PropertyData.Add(new ContentPropertyValue
                {
                    ContentId = content.Id,
                    ContentTypePropertyId = propertySnapshot.ContentTypePropertyId,
                    Alias = propertySnapshot.Alias,
                    Value = propertySnapshot.Value,
                    DateCreated = DateTime.UtcNow,
                    DateUpdated = DateTime.UtcNow
                });
            }
        }

        // Remove properties that no longer exist in the version
        var propertyIdsInVersion = version.PropertySnapshots.Select(p => p.ContentTypePropertyId).ToHashSet();
        var propertiesToRemove = content.PropertyData.Where(p => !propertyIdsInVersion.Contains(p.ContentTypePropertyId)).ToList();
        foreach (var property in propertiesToRemove)
        {
            dbContext.ContentPropertyValues.Remove(property);
        }
    }

    private static ContentSnapshot CreateContentSnapshot(Models.Content content)
    {
        return new ContentSnapshot
        {
            Name = content.Name,
            Url = content.Url(),
            ContentTypeId = content.ContentTypeId,
            ContentTypeAlias = content.ContentTypeAlias,
            DateUpdated = DateTime.UtcNow,
            Published = content.Published,
            HideFromNavigation = content.HideFromNavigation,
            LanguageId = content.LanguageId,
            ParentId = content.ParentId,
            RelatedContentId = content.RelatedContentId,
            InternalRedirectId = content.InternalRedirectId,
            IsNestedContent = content.IsNestedContent,
            Path = [..content.Path],
            SortOrder = content.SortOrder
        };
    }

    private static int CalculateContentSize(Models.Content content)
    {
        var size = 0;
        size += content.Name?.Length ?? 0;
        size += content.PropertyData.Sum(p => p.Value.Length);
        return size;
    }

    private static List<ContentDifference> CompareSnapshots(ContentSnapshot snapshot1, ContentSnapshot snapshot2)
    {
        var differences = new List<ContentDifference>();

        if (snapshot1.Name != snapshot2.Name)
        {
            differences.Add(new ContentDifference
            {
                Field = "Name",
                OldValue = snapshot1.Name,
                NewValue = snapshot2.Name
            });
        }

        if (snapshot1.Url != snapshot2.Url)
        {
            differences.Add(new ContentDifference
            {
                Field = "Url",
                OldValue = snapshot1.Url,
                NewValue = snapshot2.Url
            });
        }

        // Add other field comparisons...

        return differences;
    }

    private static List<PropertyDifference> ComparePropertySnapshots(List<ContentPropertySnapshot> props1, List<ContentPropertySnapshot> props2)
    {
        var differences = new List<PropertyDifference>();

        var props1Dict = props1.ToDictionary(p => p.ContentTypePropertyId);
        var props2Dict = props2.ToDictionary(p => p.ContentTypePropertyId);

        // Find changed properties
        foreach (var prop1 in props1)
        {
            if (props2Dict.TryGetValue(prop1.ContentTypePropertyId, out var prop2))
            {
                if (prop1.Value != prop2.Value)
                {
                    differences.Add(new PropertyDifference
                    {
                        Alias = prop1.Alias,
                        OldValue = prop1.Value,
                        NewValue = prop2.Value
                    });
                }
            }
        }

        return differences;
    }

    private List<BlockListDifference> CompareBlockListSnapshots(ContentVersion version1, ContentVersion version2)
    {
        var differences = new List<BlockListDifference>();

        // Group snapshots by property alias (we need to know which property each snapshot belongs to)
        // This is tricky because snapshots don't store which property they came from
        // We need to find BlockListEditor properties and match snapshots to them

        // First value per alias, as the page reads it (ZA-028: older data can repeat an alias)
        var version1PropsByAlias = version1.PropertySnapshots.ToValuesByAlias(p => p.Alias, p => p);
        var version2PropsByAlias = version2.PropertySnapshots.ToValuesByAlias(p => p.Alias, p => p);

        // Find properties that exist in both versions and are BlockListEditor properties (JSON arrays)
        var commonPropertyAliases = version1PropsByAlias.Keys.Intersect(version2PropsByAlias.Keys);

        foreach (var propertyAlias in commonPropertyAliases)
        {
            var prop1 = version1PropsByAlias[propertyAlias];
            var prop2 = version2PropsByAlias[propertyAlias];

            // Check if this property contains JSON array (BlockListEditor property)
            if (IsBlockListProperty(prop1.Value) || IsBlockListProperty(prop2.Value))
            {
                var contentChanges = CompareBlockListProperty(
                    prop1, prop2,
                    version1.BlockListSnapshots, version2.BlockListSnapshots);

                if (contentChanges.Any())
                {
                    differences.Add(new BlockListDifference
                    {
                        PropertyAlias = propertyAlias,
                        PropertyName = prop1.Alias,
                        ContentChanges = contentChanges
                    });
                }
            }
        }

        return differences;
    }

    private bool IsBlockListProperty(string value)
    {
        return !string.IsNullOrWhiteSpace(value) &&
               value.TrimStart().StartsWith('[') &&
               value.TrimEnd().EndsWith(']');
    }

    private List<BlockListContentChange> CompareBlockListProperty(
        ContentPropertySnapshot prop1, ContentPropertySnapshot prop2,
        List<BlockListContentSnapshot> snapshots1, List<BlockListContentSnapshot> snapshots2)
    {
        var changes = new List<BlockListContentChange>();

        try
        {
            // Parse the JSON arrays to get content IDs
            var contentIds1 = ParseContentIds(prop1.Value);
            var contentIds2 = ParseContentIds(prop2.Value);

            // Create dictionaries for quick lookup
            var snapshots1Dict = snapshots1.ToDictionary(s => s.ContentId);
            var snapshots2Dict = snapshots2.ToDictionary(s => s.ContentId);

            // Find added content (in version2 but not version1)
            var addedIds = contentIds2.Except(contentIds1);
            foreach (var contentId in addedIds)
            {
                if (snapshots2Dict.TryGetValue(contentId, out var snapshot) && snapshot != null)
                {
                    changes.Add(new BlockListContentChange
                    {
                        ChangeType = BlockListContentChangeType.Added,
                        ContentName = snapshot.ContentSnapshot.Name ?? "Unnamed Content",
                        ContentTypeAlias = snapshot.ContentSnapshot.ContentTypeAlias ?? "Unknown",
                        ContentId = contentId
                    });
                }
            }

            // Find removed content (in version1 but not version2)
            var removedIds = contentIds1.Except(contentIds2);
            foreach (var contentId in removedIds)
            {
                if (snapshots1Dict.TryGetValue(contentId, out var snapshot) && snapshot != null)
                {
                    changes.Add(new BlockListContentChange
                    {
                        ChangeType = BlockListContentChangeType.Removed,
                        ContentName = snapshot.ContentSnapshot.Name ?? "Unnamed Content",
                        ContentTypeAlias = snapshot.ContentSnapshot.ContentTypeAlias ?? "Unknown",
                        ContentId = contentId
                    });
                }
            }

            // Find modified content (in both versions, but content changed)
            var commonIds = contentIds1.Intersect(contentIds2);
            foreach (var contentId in commonIds)
            {
                if (snapshots1Dict.TryGetValue(contentId, out var snapshot1) && snapshot1 != null &&
                    snapshots2Dict.TryGetValue(contentId, out var snapshot2) && snapshot2 != null)
                {
                    var contentDiffs = CompareSnapshots(snapshot1.ContentSnapshot, snapshot2.ContentSnapshot);
                    var propertyDiffs = ComparePropertySnapshots(snapshot1.PropertySnapshots, snapshot2.PropertySnapshots);

                    if (contentDiffs.Any() || propertyDiffs.Any())
                    {
                        changes.Add(new BlockListContentChange
                        {
                            ChangeType = BlockListContentChangeType.Modified,
                            ContentName = snapshot2.ContentSnapshot.Name ?? "Unnamed Content",
                            ContentTypeAlias = snapshot2.ContentSnapshot.ContentTypeAlias ?? "Unknown",
                            PropertyChanges = propertyDiffs,
                            ContentId = contentId
                        });
                    }
                }
            }
        }
        catch (Exception)
        {
            // If parsing fails, skip this property
        }

        return changes;
    }

    private List<Guid> ParseContentIds(string jsonValue)
    {
        try
        {
            return System.Text.Json.JsonSerializer.Deserialize<List<Guid>>(jsonValue) ?? [];
        }
        catch
        {
            return [];
        }
    }

    /// <summary>
    /// Snapshots of every block the content reaches, through the nested content reference providers. A draft version
    /// records the blocks' drafts where they have one, as the editor shows them.
    /// </summary>
    private async Task<List<BlockListContentSnapshot>> CreateBlockListSnapshotsAsync(IZauberDbContext dbContext,
        Models.Content content, bool useDrafts, CancellationToken cancellationToken)
    {
        var tree = new NestedContentTree(dbContext, extensionManager, logger);
        var walk = await tree.WalkAsync(content, null, useDrafts, cancellationToken);
        if (walk.Truncated)
        {
            logger.LogWarning("The version of content {ContentId} holds only the first {MaxCount} blocks", content.Id,
                MaxSnapshotsPerVersion);
        }

        return walk.Nodes.Select(node => new BlockListContentSnapshot
        {
            ContentId = node.Id,
            ContentSnapshot = CreateContentSnapshot(node.Values),
            PropertySnapshots = node.Values.PropertyData.Select(p => new ContentPropertySnapshot
            {
                ContentTypePropertyId = p.ContentTypePropertyId,
                Alias = p.Alias,
                Value = p.Value,
                DateCreated = p.DateCreated ?? DateTime.UtcNow,
                DateUpdated = p.DateUpdated ?? DateTime.UtcNow
            }).ToList()
        }).ToList();
    }

    private async Task RestoreBlockListContentAsync(IZauberDbContext dbContext, ContentVersion version, CancellationToken cancellationToken)
    {
        // Cache valid ContentTypeProperty IDs per ContentType so schema-drift filtering doesn't N+1.
        var contentTypePropertyIdCache = new Dictionary<Guid, HashSet<Guid>>();

        async Task<HashSet<Guid>> GetValidPropertyIdsAsync(Guid contentTypeId)
        {
            if (contentTypePropertyIdCache.TryGetValue(contentTypeId, out var cached)) return cached;

            var contentType = await dbContext.ContentTypes
                .AsNoTracking()
                .FirstOrDefaultAsync(ct => ct.Id == contentTypeId, cancellationToken);

            var ids = contentType?.ContentProperties.Select(p => p.Id).ToHashSet() ?? new HashSet<Guid>();
            contentTypePropertyIdCache[contentTypeId] = ids;
            return ids;
        }

        foreach (var blockListSnapshot in version.BlockListSnapshots)
        {
            if (!blockListSnapshot.ContentSnapshot.IsNestedContent)
            {
                // Versions made before blocks were read through their editor also hold pages a picker pointed at;
                // a restore of this page must not overwrite those
                logger.LogWarning("Restore of content {ContentId}: skipping {SnapshotId}, which is not a block",
                    version.ContentId, blockListSnapshot.ContentId);
                continue;
            }

            // Always restore the content from the snapshot - this ensures the block list content
            // matches exactly what was saved in the version. Loaded without filtering soft-deletes
            // so a previously-deleted block can be brought back.
            var existingContent = await dbContext.Contents
                .Include(c => c.PropertyData)
                .FirstOrDefaultAsync(c => c.Id == blockListSnapshot.ContentId, cancellationToken);

            // Skip property snapshots that no longer correspond to a property on the current
            // ContentType (schema drifted since the snapshot was taken). Failing the restore for
            // schema drift would block normal use; warn-and-skip is the safer default.
            var validPropertyIds = await GetValidPropertyIdsAsync(blockListSnapshot.ContentSnapshot.ContentTypeId);
            var validPropertySnapshots = blockListSnapshot.PropertySnapshots
                .Where(p => validPropertyIds.Count == 0 || validPropertyIds.Contains(p.ContentTypePropertyId))
                .ToList();

            var droppedCount = blockListSnapshot.PropertySnapshots.Count - validPropertySnapshots.Count;
            if (droppedCount > 0)
            {
                logger.LogWarning(
                    "Restore: dropping {DroppedCount} property snapshot(s) on nested content {ContentId} — corresponding ContentTypeProperty no longer exists on type {ContentTypeId}",
                    droppedCount, blockListSnapshot.ContentId, blockListSnapshot.ContentSnapshot.ContentTypeId);
            }

            if (existingContent == null)
            {
                // Create new content from snapshot
                existingContent = new Models.Content
                {
                    Id = blockListSnapshot.ContentId,
                    Name = blockListSnapshot.ContentSnapshot.Name,
#pragma warning disable CS0618 // Type or member is obsolete
                    Url = blockListSnapshot.ContentSnapshot.Url, // Restore URL from snapshot
#pragma warning restore CS0618 // Type or member is obsolete
                    ContentTypeId = blockListSnapshot.ContentSnapshot.ContentTypeId,
                    ContentTypeAlias = blockListSnapshot.ContentSnapshot.ContentTypeAlias,
                    DateUpdated = blockListSnapshot.ContentSnapshot.DateUpdated,
                    Published = blockListSnapshot.ContentSnapshot.Published,
                    HideFromNavigation = blockListSnapshot.ContentSnapshot.HideFromNavigation,
                    LanguageId = blockListSnapshot.ContentSnapshot.LanguageId,
                    ParentId = blockListSnapshot.ContentSnapshot.ParentId,
                    RelatedContentId = blockListSnapshot.ContentSnapshot.RelatedContentId,
                    InternalRedirectId = blockListSnapshot.ContentSnapshot.InternalRedirectId,
                    IsNestedContent = blockListSnapshot.ContentSnapshot.IsNestedContent,
                    Path = blockListSnapshot.ContentSnapshot.Path,
                    SortOrder = blockListSnapshot.ContentSnapshot.SortOrder,
                    DateCreated = DateTime.UtcNow,
                    LastUpdatedById = version.CreatedById,
                    Deleted = false
                };

                // Add property data from snapshot (filtered for schema drift)
                foreach (var propSnapshot in validPropertySnapshots)
                {
                    existingContent.PropertyData.Add(new ContentPropertyValue
                    {
                        ContentId = existingContent.Id,
                        ContentTypePropertyId = propSnapshot.ContentTypePropertyId,
                        Alias = propSnapshot.Alias,
                        Value = propSnapshot.Value,
                        DateCreated = propSnapshot.DateCreated,
                        DateUpdated = propSnapshot.DateUpdated
                    });
                }

                dbContext.Contents.Add(existingContent);
            }
            else
            {
                // Update existing content from snapshot — also un-soft-deletes if it was previously
                // soft-deleted, so restored blocks become visible to QueryContentAsync again.
                existingContent.Name = blockListSnapshot.ContentSnapshot.Name;
#pragma warning disable CS0618 // Type or member is obsolete
                existingContent.Url = blockListSnapshot.ContentSnapshot.Url; // Restore URL from snapshot
#pragma warning restore CS0618 // Type or member is obsolete
                existingContent.ContentTypeId = blockListSnapshot.ContentSnapshot.ContentTypeId;
                existingContent.ContentTypeAlias = blockListSnapshot.ContentSnapshot.ContentTypeAlias;
                existingContent.DateUpdated = blockListSnapshot.ContentSnapshot.DateUpdated;
                existingContent.Published = blockListSnapshot.ContentSnapshot.Published;
                existingContent.HideFromNavigation = blockListSnapshot.ContentSnapshot.HideFromNavigation;
                existingContent.LanguageId = blockListSnapshot.ContentSnapshot.LanguageId;
                existingContent.ParentId = blockListSnapshot.ContentSnapshot.ParentId;
                existingContent.RelatedContentId = blockListSnapshot.ContentSnapshot.RelatedContentId;
                existingContent.InternalRedirectId = blockListSnapshot.ContentSnapshot.InternalRedirectId;
                existingContent.IsNestedContent = blockListSnapshot.ContentSnapshot.IsNestedContent;
                existingContent.Path = blockListSnapshot.ContentSnapshot.Path;
                existingContent.SortOrder = blockListSnapshot.ContentSnapshot.SortOrder;
                existingContent.LastUpdatedById = version.CreatedById;
                existingContent.Deleted = false;

                // Update properties: remove old ones and add snapshot versions.
                // Remove only those whose ContentTypePropertyId is in the (filtered) snapshot — leave
                // any other properties alone in case the user re-added them post-snapshot.
                var snapshotPropertyIds = validPropertySnapshots.Select(p => p.ContentTypePropertyId).ToHashSet();
                var propertiesToRemove = existingContent.PropertyData
                    .Where(p => !snapshotPropertyIds.Contains(p.ContentTypePropertyId) &&
                                validPropertyIds.Contains(p.ContentTypePropertyId))
                    .ToList();

                foreach (var property in propertiesToRemove)
                {
                    dbContext.ContentPropertyValues.Remove(property);
                }

                // Then update or add properties from snapshot
                foreach (var propSnapshot in validPropertySnapshots)
                {
                    var existingProperty = existingContent.PropertyData
                        .FirstOrDefault(p => p.ContentTypePropertyId == propSnapshot.ContentTypePropertyId);

                    if (existingProperty != null)
                    {
                        // Update existing property
                        existingProperty.Alias = propSnapshot.Alias;
                        existingProperty.Value = propSnapshot.Value;
                        existingProperty.DateUpdated = propSnapshot.DateUpdated;
                    }
                    else
                    {
                        // Add new property
                        existingContent.PropertyData.Add(new ContentPropertyValue
                        {
                            ContentId = existingContent.Id,
                            ContentTypePropertyId = propSnapshot.ContentTypePropertyId,
                            Alias = propSnapshot.Alias,
                            Value = propSnapshot.Value,
                            DateCreated = propSnapshot.DateCreated,
                            DateUpdated = propSnapshot.DateUpdated
                        });
                    }
                }
            }
        }
    }

    #endregion
}
