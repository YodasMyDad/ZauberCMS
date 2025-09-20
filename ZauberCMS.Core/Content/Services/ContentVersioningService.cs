using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ZauberCMS.Core.Content.Interfaces;
using ZauberCMS.Core.Content.Models;
using ZauberCMS.Core.Content.Parameters;
using ZauberCMS.Core.Data;
using ZauberCMS.Core.Extensions;
using ZauberCMS.Core.Membership.Models;
using ZauberCMS.Core.Plugins;
using ZauberCMS.Core.Shared.Models;
using ZauberCMS.Core.Shared.Services;

namespace ZauberCMS.Core.Content.Services;

public class ContentVersioningService(
    IServiceProvider serviceProvider,
    ICacheService cacheService,
    AuthenticationStateProvider authenticationStateProvider,
    UserManager<User> userManager,
    ExtensionManager extensionManager)
    : IContentVersioningService
{
    private readonly IServiceProvider _serviceProvider = serviceProvider;

    /// <summary>
    /// Creates a new version of content
    /// </summary>
    public async Task<HandlerResult<ContentVersion>> CreateVersionAsync(CreateContentVersionParameters parameters, CancellationToken cancellationToken = default)
    {
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IZauberDbContext>();
        var authState = await authenticationStateProvider.GetAuthenticationStateAsync();
        var user = await userManager.GetUserAsync(authState.User);
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
            CreatedById = user?.Id,
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
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IZauberDbContext>();
        var authState = await authenticationStateProvider.GetAuthenticationStateAsync();
        var user = await userManager.GetUserAsync(authState.User);
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
        version.CreatedById = user?.Id; // Update the publisher

        // Publish to content
        PublishVersionToContentAsync(dbContext, version, content, cancellationToken);

        var result = await dbContext.SaveChangesAndLog(version, handlerResult, cacheService, extensionManager, cancellationToken);

        // Clear content cache
        cacheService.ClearCachedItemsWithPrefix($"Content_{content.Id}");

        return result;
    }

    /// <summary>
    /// Gets versions for a content item
    /// </summary>
    public async Task<PaginatedList<ContentVersion>> GetContentVersionsAsync(QueryContentVersionsParameters parameters, CancellationToken cancellationToken = default)
    {
        using var scope = _serviceProvider.CreateScope();
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
        using var scope = _serviceProvider.CreateScope();
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
        using var scope = _serviceProvider.CreateScope();
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
            PropertyDifferences = ComparePropertySnapshots(version1.PropertySnapshots, version2.PropertySnapshots)
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

    #endregion
}
