using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ZauberCMS.Core.Content.Interfaces;
using ZauberCMS.Core.Content.Models;
using ZauberCMS.Core.Content.Parameters;
using ZauberCMS.Core.Data;
using ZauberCMS.Core.Extensions;
using ZauberCMS.Core.Plugins;
using ZauberCMS.Core.Shared.Models;
using ZauberCMS.Core.Shared.Services;

namespace ZauberCMS.Core.Content.Services;

public class ContentVersioningService(
    IServiceScopeFactory serviceScopeFactory,
    ICacheService cacheService,
    ExtensionManager extensionManager)
    : IContentVersioningService
{
    private readonly IServiceScopeFactory _serviceScopeFactory = serviceScopeFactory;

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

        var content = await dbContext.Contents
            .Include(c => c.PropertyData)
            .Include(c => c.ContentType)
            .FirstOrDefaultAsync(c => c.Id == parameters.Content.Id, cancellationToken);

        if (content == null)
        {
            handlerResult.AddMessage("Content not found", ResultMessageType.Error);
            return handlerResult;
        }

        // Get next version number
        var maxVersion = await dbContext.ContentVersions
            .Where(v => v.ContentId == content.Id)
            .MaxAsync(v => (int?)v.VersionNumber, cancellationToken) ?? 0;

        // Clear existing flags if this is a new published version
        if (parameters.Status == ContentVersionStatus.Published)
        {
            await ClearCurrentPublishedFlagAsync(dbContext, content.Id, cancellationToken);
        }

        // Clear existing latest draft flag if this is a new draft
        if (parameters.Status == ContentVersionStatus.Draft)
        {
            await ClearLatestDraftFlagAsync(dbContext, content.Id, cancellationToken);
        }

        // Version creation logic

        var version = new ContentVersion
        {
            ContentId = content.Id,
            VersionNumber = maxVersion + 1,
            VersionName = parameters.VersionName,
            Status = parameters.Status,
            Comments = parameters.Comments,
            CreatedById = parameters.CreatedByUserId,
            IsCurrentPublished = parameters.Status == ContentVersionStatus.Published,
            IsLatestDraft = parameters.Status == ContentVersionStatus.Draft,
            IsAutoSave = parameters.IsAutoSave,
            ParentVersionId = parameters.ParentVersionId,
            Tags = parameters.Tags ?? [],
            Snapshot = CreateContentSnapshot(content),
            PropertySnapshots = content.PropertyData?.Select(p => new ContentPropertySnapshot
            {
                ContentTypePropertyId = p.ContentTypePropertyId,
                Alias = p.Alias,
                Value = p.Value,
                DateCreated = p.DateCreated ?? DateTime.UtcNow,
                DateUpdated = p.DateUpdated ?? DateTime.UtcNow
            }).ToList() ?? [],
            BlockListSnapshots = await CreateBlockListSnapshotsAsync(dbContext, content),
            ContentSize = CalculateContentSize(content)
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

        dbContext.ContentVersions.Add(version);

        // Update content if this is being published
        if (parameters.Status == ContentVersionStatus.Published)
        {
            PublishVersionToContentAsync(dbContext, version, content, cancellationToken);
        }

        var result = await dbContext.SaveChangesAndLog(version, handlerResult, cacheService, extensionManager, cancellationToken);

        // Invalidate content cache
        cacheService.ClearCachedItemsWithPrefix($"Content_{content.Id}");

        return result;
    }

    /// <summary>
    /// Publishes a specific version
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

        // Clear existing published flag
        await ClearCurrentPublishedFlagAsync(dbContext, content.Id, cancellationToken);

        // Update version
        version.Status = ContentVersionStatus.Published;
        version.IsCurrentPublished = true;
        version.DatePublished = DateTime.UtcNow;
        version.CreatedById = parameters.PublishedByUserId; // Update the publisher

        // Publish to content
        PublishVersionToContentAsync(dbContext, version, content, cancellationToken);

        // Restore block list content from snapshots
        await RestoreBlockListContentAsync(dbContext, version, cancellationToken);

        // Save all changes
        await dbContext.SaveChangesAsync(cancellationToken);

        // Update the result to indicate success
        handlerResult.Entity = version;
        handlerResult.Success = true;

        // Clear content cache
        cacheService.ClearCachedItemsWithPrefix($"Content_{content.Id}");

        return handlerResult;
    }

    /// <summary>
    /// Gets versions for a content item
    /// </summary>
    public async Task<PaginatedList<ContentVersion>> GetContentVersionsAsync(QueryContentVersionsParameters parameters, CancellationToken cancellationToken = default)
    {
        using var scope = _serviceScopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IZauberDbContext>();

        var query = dbContext.ContentVersions
            .Include(v => v.CreatedBy)
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
        var items = await query
            .Skip((parameters.PageIndex - 1) * parameters.AmountPerPage)
            .Take(parameters.AmountPerPage)
            .ToListAsync(cancellationToken);

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
        await dbContext.SaveChangesAsync(cancellationToken);

        handlerResult.Success = true;
        handlerResult.Entity = true;

        return handlerResult;
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

    private async Task ClearCurrentPublishedFlagAsync(IZauberDbContext dbContext, Guid contentId, CancellationToken cancellationToken)
    {
        var currentPublished = await dbContext.ContentVersions
            .FirstOrDefaultAsync(v => v.ContentId == contentId && v.IsCurrentPublished, cancellationToken);

        if (currentPublished != null)
        {
            currentPublished.IsCurrentPublished = false;
        }
    }

    private async Task ClearLatestDraftFlagAsync(IZauberDbContext dbContext, Guid contentId, CancellationToken cancellationToken)
    {
        var latestDraft = await dbContext.ContentVersions
            .FirstOrDefaultAsync(v => v.ContentId == contentId && v.IsLatestDraft, cancellationToken);

        if (latestDraft != null)
        {
            latestDraft.IsLatestDraft = false;
        }
    }

    private void PublishVersionToContentAsync(IZauberDbContext dbContext, ContentVersion version, Models.Content content, CancellationToken cancellationToken)
    {
        // Update content from snapshot
        content.Name = version.Snapshot.Name;
        // Note: URL is handled by the system and shouldn't be directly set from version
        content.ContentTypeId = version.Snapshot.ContentTypeId;
        content.ContentTypeAlias = version.Snapshot.ContentTypeAlias;
        content.DateUpdated = version.Snapshot.DateUpdated;
        content.Published = version.Snapshot.Published;
        content.HideFromNavigation = version.Snapshot.HideFromNavigation;
        content.LanguageId = version.Snapshot.LanguageId;
        content.ParentId = version.Snapshot.ParentId;
        content.Path = version.Snapshot.Path;
        content.SortOrder = version.Snapshot.SortOrder;

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

        var version1PropsByAlias = version1.PropertySnapshots.ToDictionary(p => p.Alias);
        var version2PropsByAlias = version2.PropertySnapshots.ToDictionary(p => p.Alias);

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

    private static async Task<List<BlockListContentSnapshot>> CreateBlockListSnapshotsAsync(IZauberDbContext dbContext, Models.Content content)
    {
        var snapshots = new List<BlockListContentSnapshot>();
        var processedContentIds = new HashSet<Guid>(); // Prevent infinite recursion

        await CreateBlockListSnapshotsRecursiveAsync(dbContext, content, snapshots, processedContentIds);

        return snapshots;
    }

    private static async Task CreateBlockListSnapshotsRecursiveAsync(IZauberDbContext dbContext, Models.Content content, List<BlockListContentSnapshot> snapshots, HashSet<Guid> processedContentIds)
    {
        // Find all block list properties in the content
        var blockListProperties = content.PropertyData.Where(p =>
            !string.IsNullOrEmpty(p.Value) &&
            p.Value.TrimStart().StartsWith('[') &&
            p.Value.TrimEnd().EndsWith(']'));

        foreach (var blockListProperty in blockListProperties)
        {
            try
            {
                // Parse the JSON array of content IDs
                var contentIds = System.Text.Json.JsonSerializer.Deserialize<List<Guid>>(blockListProperty.Value);
                if (contentIds != null && contentIds.Any())
                {
                    // Get all the block list content items
                    var blockListContent = await dbContext.Contents
                        .Include(c => c.PropertyData)
                        .Where(c => contentIds.Contains(c.Id))
                        .ToListAsync();

                    // Create snapshots for each content item
                    foreach (var blockContent in blockListContent)
                    {
                        // Skip if already processed to prevent infinite recursion
                        if (processedContentIds.Contains(blockContent.Id))
                        {
                            continue;
                        }

                        processedContentIds.Add(blockContent.Id);

                        snapshots.Add(new BlockListContentSnapshot
                        {
                            ContentId = blockContent.Id,
                            ContentSnapshot = CreateContentSnapshot(blockContent),
                            PropertySnapshots = blockContent.PropertyData?.Select(p => new ContentPropertySnapshot
                            {
                                ContentTypePropertyId = p.ContentTypePropertyId,
                                Alias = p.Alias,
                                Value = p.Value,
                                DateCreated = p.DateCreated ?? DateTime.UtcNow,
                                DateUpdated = p.DateUpdated ?? DateTime.UtcNow
                            }).ToList() ?? []
                        });

                        // Recursively process nested block list content
                        await CreateBlockListSnapshotsRecursiveAsync(dbContext, blockContent, snapshots, processedContentIds);
                    }
                }
            }
            catch (System.Text.Json.JsonException)
            {
                // Skip invalid JSON
                continue;
            }
        }
    }

    private async Task RestoreBlockListContentAsync(IZauberDbContext dbContext, ContentVersion version, CancellationToken cancellationToken)
    {
        foreach (var blockListSnapshot in version.BlockListSnapshots)
        {
            // Always restore the content from the snapshot - this ensures the block list content
            // matches exactly what was saved in the version
            var existingContent = await dbContext.Contents
                .Include(c => c.PropertyData)
                .FirstOrDefaultAsync(c => c.Id == blockListSnapshot.ContentId, cancellationToken);

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
                    IsNestedContent = blockListSnapshot.ContentSnapshot.IsNestedContent,
                    Path = blockListSnapshot.ContentSnapshot.Path,
                    SortOrder = blockListSnapshot.ContentSnapshot.SortOrder,
                    DateCreated = DateTime.UtcNow,
                    LastUpdatedById = version.CreatedById
                };

                // Add property data from snapshot
                foreach (var propSnapshot in blockListSnapshot.PropertySnapshots)
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
                // Update existing content from snapshot
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
                existingContent.IsNestedContent = blockListSnapshot.ContentSnapshot.IsNestedContent;
                existingContent.Path = blockListSnapshot.ContentSnapshot.Path;
                existingContent.SortOrder = blockListSnapshot.ContentSnapshot.SortOrder;
                existingContent.LastUpdatedById = version.CreatedById;

                // Update properties: remove old ones and add snapshot versions
                // First, remove properties that don't exist in the snapshot
                var snapshotPropertyIds = blockListSnapshot.PropertySnapshots.Select(p => p.ContentTypePropertyId).ToHashSet();
                var propertiesToRemove = existingContent.PropertyData
                    .Where(p => !snapshotPropertyIds.Contains(p.ContentTypePropertyId))
                    .ToList();

                foreach (var property in propertiesToRemove)
                {
                    dbContext.ContentPropertyValues.Remove(property);
                }

                // Then update or add properties from snapshot
                foreach (var propSnapshot in blockListSnapshot.PropertySnapshots)
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
