using System.Linq.Dynamic.Core;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
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
using ZauberCMS.Core.Shared.Services;
using ZauberCMS.Core.Membership.Models;
using ZauberCMS.Core.Shared;
using System.Text.Json;

namespace ZauberCMS.Core.Content.Services;

public class ContentService(
    IServiceScopeFactory serviceScopeFactory,
    ICacheService cacheService,
    IOptions<ZauberSettings> settings,
    AuthenticationStateProvider authenticationStateProvider,
    UserManager<User> userManager,
    ExtensionManager extensionManager,
    AppState appState,
    ILanguageService languageService,
    ILogger<ContentService> logger)
    : IContentService
{
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
        var cacheKey = query.GenerateCacheKey();
        if (parameters.Cached)
        {
            return await cacheService.GetSetCachedItemAsync(cacheKey,
                async () => await FetchContentAsync(query, cancellationToken));
        }

        return await FetchContentAsync(query, cancellationToken);
    }

    /// <summary>
    /// Creates or updates a content item, including property data and roles. Logs audit entries and invalidates cache.
    /// </summary>
    /// <param name="parameters">The content to save and related options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result including success state and messages.</returns>
    public async Task<HandlerResult<Models.Content>> SaveContentAsync(SaveContentParameters parameters,
        CancellationToken cancellationToken = default)
    {
        using var scope = serviceScopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IZauberDbContext>();
        var authState = await authenticationStateProvider.GetAuthenticationStateAsync();
        User? user = await userManager.GetUserAsync(authState.User);
        var isUpdate = true;
        var handlerResult = new HandlerResult<Models.Content>();

        if (parameters.Content == null)
        {
            handlerResult.AddMessage("Content is null", ResultMessageType.Error);
            return handlerResult;
        }

        var unpublishedContent =
            dbContext.UnpublishedContent.FirstOrDefault(x => x.Id == parameters.Content.UnpublishedContentId);

        if (parameters.SaveUnpublishedOnly)
        {
            var isNew = parameters.Content.UnpublishedContentId == null;
            unpublishedContent ??= new UnpublishedContent();
            parameters.Content.MapTo(unpublishedContent.JsonContent);
            unpublishedContent.JsonContent.PropertyData = parameters.Content.PropertyData;
            unpublishedContent.JsonContent.UnpublishedContentId = unpublishedContent.Id;

            if (isNew)
            {
                var dbContent = dbContext.Contents.FirstOrDefault(x => parameters.Content.Id == x.Id);
                if (dbContent != null)
                {
                    dbContent.UnpublishedContentId = unpublishedContent.Id;
                }

                dbContext.UnpublishedContent.Add(unpublishedContent);
            }

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

        if (parameters.Content.Url().IsNullOrWhiteSpace())
        {
            var baseSlug = new SlugHelper().GenerateSlug(parameters.Content.Name);
#pragma warning disable CS0618 // Type or member is obsolete
            parameters.Content.Url = GenerateUniqueUrl(dbContext, baseSlug);
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

        if (content == null)
        {
            isUpdate = false;
            content = parameters.Content;

            // Auto-assign sort order for new content items (only for regular content, not element types/block lists)
            if (content.SortOrder == 0 && !content.IsNestedContent)
            {
                content.SortOrder = await GetNextSortOrderAsync(dbContext, content.ParentId, content.IsRootContent, cancellationToken) + 1;
            }

            content.LastUpdatedById = user!.Id;
            dbContext.Contents.Add(content);
        }
        else
        {
            parameters.Content.MapTo(content);
            content.LastUpdatedById = user!.Id;
            content.DateUpdated = DateTime.UtcNow;

            if (!parameters.ExcludePropertyData)
            {
                UpdateContentPropertyValues(dbContext, content, parameters.Content.PropertyData);
            }
        }

        if (parameters.UpdateContentRoles)
        {
            UpdateContentRoles(dbContext, content, parameters);
        }

        if (unpublishedContent != null)
        {
            dbContext.UnpublishedContent.Remove(unpublishedContent);
        }

        content.Path = content.BuildPath(dbContext, isUpdate, settings);

        // Log audit without Mediator

        var nameText = content.Name ?? nameof(Models.Content);
        var actionText = isUpdate ? "Updated" : "Created";
        await SaveAuditAsync(dbContext, $"{user.Name} {actionText} {nameText}", cancellationToken);

        var saveResult = await dbContext.SaveChangesAndLog(content, handlerResult, cacheService, extensionManager,
            cancellationToken);

        // Recursively process BlockListEditor changes for nested content
        if (saveResult.Success && !parameters.SaveUnpublishedOnly)
        {
            await ProcessBlockListEditorChangesAsync(content, dbContext, user!, cancellationToken);
        }

        // Create version after successful save
        if (saveResult is { Success: true, Entity: not null })
        {
            try
            {
                // Get the versioning service and create a version
                var versioningService = scope.ServiceProvider.GetService<IContentVersioningService>();
                if (versioningService != null)
                {
                    // Use the parameter content which has the updated PropertyData from the UI
                    // If content is currently published, create a published version to replace it
                    // Otherwise create a draft version
                    var shouldCreatePublishedVersion = content is { Published: true };
                    var versionParameters = new CreateContentVersionParameters
                    {
                        Content = parameters.Content, // Use the original parameter content with updated values
                        Status = shouldCreatePublishedVersion
                            ? ContentVersionStatus.Published
                            : ContentVersionStatus.Draft,
                        IsAutoSave = false,
                        Comments = shouldCreatePublishedVersion
                            ? (isUpdate ? "Content updated and published" : "Content created and published")
                            : (isUpdate ? "Content updated" : "Content created"),
                        CreatedByUserId = user?.Id
                    };

                    await versioningService.CreateVersionAsync(versionParameters, cancellationToken);
                }
                else
                {
                    logger.LogWarning("ContentVersioningService is null - versioning will not work");
                }
            }
            catch (Exception ex)
            {
                // Log version creation error but don't fail the content save
                logger.LogError(ex, "Failed to create content version for content {ContentId}: {ErrorMessage}", 
                    content.Id, ex.Message);
            }
        }

        return saveResult;
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
        var cacheKey = $"{query.GenerateCacheKey(typeof(Models.Content))}_Page{parameters.PageIndex}_Amount{parameters.AmountPerPage}";
        if (parameters.Cached)
        {
            return (await cacheService.GetSetCachedItemAsync(cacheKey,
                async () => await FetchContentAsync(query, parameters)))!;
        }

        return await FetchContentAsync(query, parameters);
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
        var authState = await authenticationStateProvider.GetAuthenticationStateAsync();
        var loggedInUser = await userManager.GetUserAsync(authState.User);
        var handlerResult = new HandlerResult<Models.Content>();

        var content = dbContext.Contents.FirstOrDefault(x => x.Id == parameters.ContentId);
        if (content == null)
        {
            handlerResult.AddMessage("Unable to delete, as no Content with that id exists", ResultMessageType.Warning);
            return handlerResult;
        }

        if (parameters.MoveToRecycleBin)
        {
            content.Deleted = true;
            await SaveAuditIfUser(dbContext, loggedInUser, content.Name, "Recycle Binned", cancellationToken);
        }
        else
        {
            var children = dbContext.Contents.AsNoTracking().Where(x => x.ParentId == content.Id);
            if (await children.AnyAsync(cancellationToken))
            {
                handlerResult.AddMessage(
                    "Unable to delete content with child content, delete or move those items first",
                    ResultMessageType.Error);
                return handlerResult;
            }

            var propertyDataToDelete = dbContext.ContentPropertyValues.Where(x => x.ContentId == content.Id);
            dbContext.ContentPropertyValues.RemoveRange(propertyDataToDelete);

            if (content.UnpublishedContentId != null)
            {
                var uContent = dbContext.UnpublishedContent.FirstOrDefault(x => x.Id == content.UnpublishedContentId);
                if (uContent != null) dbContext.UnpublishedContent.Remove(uContent);
            }

            content.PropertyData.Clear();
            await SaveAuditIfUser(dbContext, loggedInUser, content.Name, "Deleted", cancellationToken);
            dbContext.Contents.Remove(content);
            await appState.NotifyContentDeleted(null, authState.User.Identity?.Name!);
        }

        return await dbContext.SaveChangesAndLog(content, handlerResult, cacheService, extensionManager,
            cancellationToken);
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
        var authState = await authenticationStateProvider.GetAuthenticationStateAsync();
        var user = await userManager.GetUserAsync(authState.User);
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
        var newParentId = parameters.CopyTo ?? contentToCopy.ParentId;
        var copiedContent = CreateCopy(contentToCopy, user, newParentId);

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
            var descendants = await dbContext.Contents
                .WherePathLike(contentToCopy.Id)
                .AsNoTracking()
                .Include(content => content.PropertyData)
                .ToListAsync(cancellationToken);

            foreach (var descendant in descendants.Where(x => x.Id != contentToCopy.Id))
            {
                if (descendant.ParentId != null)
                {
                    var newParentIdForDescendant = idMap[descendant.ParentId.Value];
                    var copiedDescendant = CreateCopy(descendant, user, newParentIdForDescendant);
                    copiedDescendant.Path = descendant.Path
                        .Select(id => idMap.TryGetValue(id, out var value) ? value : id).ToList();
                    idMap[descendant.Id] = copiedDescendant.Id;
                    dbContext.Add(copiedDescendant);
                }
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        if (user != null)
        {
            await SaveAuditAsync(dbContext, $"{user.Name} Copied {contentToCopy.Name}", cancellationToken);
        }

        handlerResult.Success = true;
        handlerResult.AddMessage("Content copied successfully.", ResultMessageType.Success);
        return handlerResult;

        Models.Content CreateCopy(Models.Content original, User? currentUser, Guid? parentId = null)
        {
            var copy = original.MapToNew();
            copy.Id = Guid.NewGuid();
            copy.Name = original.Name + " (Copy)";
#pragma warning disable CS0618 // Type or member is obsolete
            copy.Url = original.Url + "-copy";
#pragma warning restore CS0618 // Type or member is obsolete
            copy.LastUpdatedById = currentUser?.Id;
            copy.ParentId = parentId;
            copy.DateCreated = DateTime.UtcNow;
            copy.DateUpdated = DateTime.UtcNow;
            copy.Path = [];
            copy.Published = false;
            copy.Deleted = false;
            copy.PropertyData = original.PropertyData.Select(p => new ContentPropertyValue
            {
                Id = Guid.NewGuid(),
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
        var cacheKey = parameters.GenerateCacheKey<Models.Content>("GetContentFromRequest");
        return (await cacheService.GetSetCachedItemAsync(cacheKey, async () =>
        {
            using var scope = serviceScopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<IZauberDbContext>();
            return await FetchEntryModelAsync(parameters, dbContext, cancellationToken);
        }, 0, 5))!;
    }

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
        var authState = await authenticationStateProvider.GetAuthenticationStateAsync();
        var user = await userManager.GetUserAsync(authState.User);
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

        var contentType = dbContext.ContentTypes.FirstOrDefault(x => x.Id == parameters.ContentType.Id);
        if (contentType == null)
        {
            var containsAlias = dbContext.ContentTypes.Any(x => x.Alias == parameters.ContentType.Alias);
            if (containsAlias)
            {
                handlerResult.AddMessage("Content Type Alias already exists, change the content type name",
                    ResultMessageType.Error);
                return handlerResult;
            }

            contentType = parameters.ContentType;
            contentType.LastUpdatedById = user!.Id;
            dbContext.ContentTypes.Add(contentType);
        }
        else
        {
            isUpdate = true;
            parameters.ContentType.MapTo(contentType);
            contentType.LastUpdatedById = user!.Id;
            contentType.DateUpdated = DateTime.UtcNow;
        }

        var actionText = isUpdate ? "Updated" : "Created";
        await SaveAuditAsync(dbContext, $"{user.Name} {actionText} {contentType.Name}", cancellationToken);

        return await dbContext.SaveChangesAndLog(contentType, handlerResult, cacheService, extensionManager,
            cancellationToken);
    }

    /// <summary>
    /// Queries content types with filtering and paging.
    /// </summary>
    /// <param name="parameters">Query options including filters and paging.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Paged list of content types.</returns>
    public Task<PaginatedList<ContentType>> QueryContentTypesAsync(QueryContentTypesParameters parameters,
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
                query = query.Where(x => x.Name != null && x.Name.ToLower().Contains(parameters.SearchTerm.ToLower()));
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

        return Task.FromResult(query.ToPaginatedList(parameters.PageIndex, parameters.AmountPerPage));
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
        var authState = await authenticationStateProvider.GetAuthenticationStateAsync();
        var user = await userManager.GetUserAsync(authState.User);
        var handlerResult = new HandlerResult<ContentType>();

        // Check for regular content using this ContentType
        var contentUsingContentType =
            await QueryContentAsync(new QueryContentParameters { ContentTypeId = parameters.ContentTypeId },
                cancellationToken);
        if (contentUsingContentType.Items.Any())
        {
            handlerResult.Success = false;
            handlerResult.AddMessage("Unable to delete, because this ContentType is being used by content items",
                ResultMessageType.Warning);
            return handlerResult;
        }

        // Check for nested content using this ContentType (Element Types)
        var nestedContentUsingContentType =
            await QueryContentAsync(new QueryContentParameters
            {
                ContentTypeId = parameters.ContentTypeId,
                NestedFilter = QueryContentParameters.NestedContentFilter.Only,
                IncludeUnpublished = true
            }, cancellationToken);
        if (nestedContentUsingContentType.Items.Any())
        {
            handlerResult.Success = false;
            var count = nestedContentUsingContentType.TotalItems;
            handlerResult.AddMessage(
                $"Unable to delete, because this Element Type is used by {count} nested content item{(count == 1 ? "" : "s")}",
                ResultMessageType.Warning);
            return handlerResult;
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

        var contentType = dbContext.ContentTypes.FirstOrDefault(x => x.Id == parameters.ContentTypeId);
        if (contentType != null)
        {
            if (contentType.IsComposition)
            {
                var anyUsingThisComposition = QueryContentTypesForComposition(dbContext, parameters.ContentTypeId);
                if (anyUsingThisComposition.Items.Any())
                {
                    handlerResult.Success = false;
                    handlerResult.AddMessage(
                        "Unable to delete, because there are content types using this composition, remove it first",
                        ResultMessageType.Warning);
                    return handlerResult;
                }
            }

            if (user != null)
            {
                await SaveAuditAsync(dbContext, $"{user.Name} Deleted {contentType.Name}", cancellationToken);
            }

            dbContext.ContentTypes.Remove(contentType);
            return await dbContext.SaveChangesAndLog(contentType, handlerResult, cacheService, extensionManager,
                cancellationToken);
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
        var authState = await authenticationStateProvider.GetAuthenticationStateAsync();
        var user = await userManager.GetUserAsync(authState.User);
        var handlerResult = new HandlerResult<Domain>();
        var isUpdate = false;

        if (parameters.Domain == null)
        {
            handlerResult.AddMessage("Domain is null", ResultMessageType.Error);
            return handlerResult;
        }

        var domain = dbContext.Domains.FirstOrDefault(x => x.Id == parameters.Domain.Id);
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

        if (user != null)
        {
            var actionText = isUpdate ? "Updated" : "Created";
            await SaveAuditAsync(dbContext, $"{user.Name} {actionText} Domain ({domain.Url})", cancellationToken);
        }

        return await dbContext.SaveChangesAndLog(domain, handlerResult, cacheService, extensionManager,
            cancellationToken);
    }

    /// <summary>
    /// Queries domains with filtering and paging.
    /// </summary>
    /// <param name="parameters">Query options including ids, where clause and ordering.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Paged list of domains.</returns>
    public Task<PaginatedList<Domain>> QueryDomainAsync(QueryDomainParameters parameters,
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
        
        return Task.FromResult(query.ToPaginatedList(parameters.PageIndex, parameters.AmountPerPage));
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
        var authState = await authenticationStateProvider.GetAuthenticationStateAsync();
        var user = await userManager.GetUserAsync(authState.User);
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
            if (user != null)
            {
                await SaveAuditAsync(dbContext, $"{user.Name} Deleted Domain ({domain.Url})", cancellationToken);
            }

            dbContext.Domains.Remove(domain);
            return await dbContext.SaveChangesAndLog(domain, handlerResult, cacheService, extensionManager,
                cancellationToken);
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
        using var scope = serviceScopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IZauberDbContext>();
        return await dbContext.Contents.AsNoTracking().AnyAsync(cancellationToken: cancellationToken);
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
        var cacheKey = parameters.GenerateCacheKey<Models.Content>("HasChild");
        if (parameters.Cached)
        {
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
        var cacheKey = parameters.GenerateCacheKey<ContentType>("HasContentTypeChild");
        if (parameters.Cached)
        {
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
                dict.Add(c.Url!, c.Language!.LanguageIsoCode!);
                dict.Add(c.Id, c.Language!.LanguageIsoCode!);
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
            var uContent =
                await dbContext.UnpublishedContent.FirstOrDefaultAsync(x => x.Id == content.UnpublishedContentId,
                    cancellationToken: cancellationToken);
            if (uContent != null) dbContext.UnpublishedContent.Remove(uContent);
            var result = (await dbContext.SaveChangesAndLog(uContent, handlerResult, cacheService, extensionManager,
                cancellationToken))!;
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
            query = query.Include(x => x.Children).AsSplitQuery();
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

        if (!string.IsNullOrEmpty(parameters.Filter))
        {
            query = query.Where(parameters.Filter);
        }

        if (!string.IsNullOrEmpty(parameters.Order))
        {
            query = query.OrderBy(parameters.Order);
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

    public async Task<string?> ExportContentTypeAsync(string alias, bool includeContent = false)
    {
        using var scope = serviceScopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IZauberDbContext>();

        var contentType = await dbContext.ContentTypes
            .Include(x => x.Tabs)
            .FirstOrDefaultAsync(x => x.Alias == alias);

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
        if (contentType.CompositionIds.Any())
        {
            exportType.CompositionAliases = await dbContext.ContentTypes
                .Where(ct => contentType.CompositionIds.Contains(ct.Id))
                .Select(ct => ct.Alias!)
                .ToListAsync();
        }

        // Map AllowedChildContentTypes to aliases
        if (contentType.AllowedChildContentTypes.Any())
        {
            exportType.AllowedChildContentTypeAliases = await dbContext.ContentTypes
                .Where(ct => contentType.AllowedChildContentTypes.Contains(ct.Id))
                .Select(ct => ct.Alias!)
                .ToListAsync();
        }

        // Map ParentId to alias
        if (contentType.ParentId.HasValue)
        {
            var parent = await dbContext.ContentTypes.FindAsync(contentType.ParentId.Value);
            exportType.ParentAlias = parent?.Alias ?? string.Empty;
        }

        var package = new ContentTypePackage { Type = exportType };

        if (includeContent)
        {
            var rootContents = await dbContext.Contents
                .Where(c => c.ContentTypeId == contentType.Id && c.ParentId == null)
                .Include(c => c.PropertyData)
                .ToListAsync();

            package.RootContents = await BuildContentExportTree(rootContents, dbContext, contentType);
        }

        return JsonSerializer.Serialize(package, new JsonSerializerOptions { WriteIndented = true });
    }

    private async Task<List<ContentExport>> BuildContentExportTree(List<Models.Content> contents,
        IZauberDbContext dbContext, ContentType contentType)
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
                .ToListAsync();
            exp.Children = await BuildContentExportTree(children, dbContext, contentType);

            exports.Add(exp);
        }

        return exports;
    }

    public async Task<HandlerResult<ContentType>> ImportContentTypeAsync(string json)
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
        var authState = await authenticationStateProvider.GetAuthenticationStateAsync();
        var user = await userManager.GetUserAsync(authState.User);

        // Import ContentType
        var export = package.Type;
        var existing = await dbContext.ContentTypes.FirstOrDefaultAsync(x => x.Alias == export.Alias);

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

        // Regenerate Tab Ids
        var tabMap = new Dictionary<string, Guid>();
        foreach (var tab in export.Tabs)
        {
            var newId = Guid.NewGuid();
            tabMap[tab.Alias] = newId;
            tab.Id = newId;
        }

        contentType.Tabs = export.Tabs;

        // Set PropertyType TabIds based on map, regenerate Ids, create alias map
        var propMap = new Dictionary<string, Guid>();
        foreach (var prop in export.ContentProperties)
        {
            var newId = Guid.NewGuid();
            prop.Id = newId;
            propMap[prop.Alias ?? prop.Name ?? string.Empty] = newId;
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
            var comp = await dbContext.ContentTypes.FirstOrDefaultAsync(x => x.Alias == compAlias);
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
            var child = await dbContext.ContentTypes.FirstOrDefaultAsync(x => x.Alias == childAlias);
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
            var parent = await dbContext.ContentTypes.FirstOrDefaultAsync(x => x.Alias == export.ParentAlias);
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
        var typeResult = await SaveContentTypeAsync(saveParams);
        if (!typeResult.Success)
        {
            return typeResult;
        }

        // If no content, done
        if (!package.RootContents.Any())
        {
            return typeResult;
        }

        // Import content recursively
        foreach (var rootExp in package.RootContents)
        {
            await ImportContentRecursive(rootExp, null, contentType, dbContext, user, propMap, handlerResult);
        }

        await dbContext.SaveChangesAsync();

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
            LastUpdatedById = user?.Id ?? Guid.Empty,
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


    private IQueryable<Models.Content> BuildQuery(GetContentParameters request, IZauberDbContext dbContext)
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
            query = request.IncludeUnpublished
                ? query.Include(x => x.Children)
                : query.Include(x => x.Children.Where(c => c.Published));
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

    private IQueryable<Models.Content> BuildQuery(QueryContentParameters request, IZauberDbContext dbContext)
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

            if (request.IsDeleted != null)
            {
                query = query.Where(x => x.Deleted == request.IsDeleted);
            }

            if (request.IncludeChildren)
            {
                query = request.IncludeUnpublished
                    ? query.Include(x => x.Children)
                        .ThenInclude(x => x.UnpublishedContent)
                    : query.Include(x => x.Children.Where(c => c.Published));
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
                query = query.Where(x => x.Name != null && x.Name.ToLower().Contains(request.SearchTerm.ToLower()));
            }

            if (!string.IsNullOrWhiteSpace(request.ContentTypeAlias))
            {
                var contentType = dbContext.ContentTypes.AsNoTracking()
                    .FirstOrDefault(x => x.Alias == request.ContentTypeAlias);
                request.ContentTypeId = contentType?.Id ?? Guid.Empty;
            }

            if (request.ContentTypeId != null)
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

        return query;
    }

    private Task<PaginatedList<Models.Content>> FetchContentAsync(IQueryable<Models.Content> query,
        QueryContentParameters request)
    {
        return Task.FromResult(query.ToPaginatedList(request.PageIndex, request.AmountPerPage));
    }

    private async Task<Models.Content?> FetchContentAsync(IQueryable<Models.Content> query,
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

        var content = request.IsRootContent
            ? matchedDomain != null
                ? await contentQueryable.Select(c => new
                        { c.Id, c.InternalRedirectId, c.ContentType!.IncludeChildren, c.Path })
                    .FirstOrDefaultAsync(x => x.Id == matchedDomain.ContentId, cancellationToken)
                : await contentQueryable.Where(c => c.IsRootContent && c.Published)
                    .Select(c => new { c.Id, c.InternalRedirectId, c.ContentType!.IncludeChildren, c.Path })
                    .FirstOrDefaultAsync(cancellationToken)
#pragma warning disable CS0618 // Type or member is obsolete
            : await contentQueryable.Where(c => c.Url == request.Slug && c.Published)
#pragma warning restore CS0618 // Type or member is obsolete
                .Select(c => new { c.Id, c.InternalRedirectId, c.ContentType!.IncludeChildren, c.Path })
                .FirstOrDefaultAsync(cancellationToken);

        if (content?.InternalRedirectId != null && content.InternalRedirectId != Guid.Empty)
        {
            var internalRedirectIdValue = content.InternalRedirectId.Value;
            content = await contentQueryable.Where(c => c.Id == internalRedirectIdValue)
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
            query = query.Include(x => x.Children);
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

            if (languageIsoCode.IsNullOrWhiteSpace())
            {
                languageIsoCode = settings.Value.AdminDefaultLanguage;
            }
        }

        entryModel.LanguageIsoCode = languageIsoCode;
        var allLanguageData = await languageService.GetCachedAllLanguageDictionariesAsync(
            new ZauberCMS.Core.Languages.Parameters.GetCachedAllLanguageDictionariesParameters(), cancellationToken);
        if (allLanguageData.TryGetValue(languageIsoCode, out var lng))
        {
            if (lng != null)
            {
                entryModel.LanguageKeys = lng;
            }
        }

        return entryModel;
    }

    private static Domain? MatchDomainWithContent(string url, List<Domain> domains)
    {
        var uri = new Uri(url);
        var requestHost = uri.Host.ToLower();
        var requestPath = uri.AbsolutePath.TrimStart('/').ToLower();
        return domains.FirstOrDefault(domain =>
        {
            var domainUrl = domain.Url?.ToLower();
            if (domainUrl != null && domainUrl.Contains('/'))
            {
                var parts = domainUrl.Split('/', 2);
                var domainHost = parts[0];
                var domainPath = parts.Length > 1 ? parts[1] : string.Empty;
                return requestHost == domainHost && requestPath.StartsWith(domainPath);
            }

            return requestHost == domainUrl;
        });
    }

    private static string GenerateUniqueUrl(IZauberDbContext dbContext, string baseSlug)
    {
        var url = baseSlug;
#pragma warning disable CS0618 // Type or member is obsolete
        if (!dbContext.Contents.Any(c => c.Url == url))
#pragma warning restore CS0618 // Type or member is obsolete
        {
            return url;
        }

        var counter = 1;
#pragma warning disable CS0618 // Type or member is obsolete
        while (dbContext.Contents.Any(c => c.Url == url))
#pragma warning restore CS0618 // Type or member is obsolete
        {
            url = $"{baseSlug}-{counter}";
            counter++;
        }

        return url;
    }

    private static void UpdateContentRoles(IZauberDbContext dbContext, Models.Content content,
        SaveContentParameters request)
    {
        var existingRoles = dbContext.ContentRoles.Where(r => r.ContentId == content.Id).ToList();
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

    private async Task<int> GetNextSortOrderAsync(IZauberDbContext dbContext, Guid? parentId, bool isRootContent, CancellationToken cancellationToken)
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

    private static PaginatedList<ContentType> QueryContentTypesForComposition(IZauberDbContext dbContext,
        Guid compositionId)
    {
        var query = dbContext.ContentTypes.WhereHasCompositionsUsing(compositionId);
        return query.ToPaginatedList(1, int.MaxValue);
    }

    private static async Task SaveAuditIfUser(IZauberDbContext dbContext, User? user, string? name, string action,
        CancellationToken cancellationToken)
    {
        if (user == null) return;
        await SaveAuditAsync(dbContext, $"{user.Name} {action} {name ?? string.Empty}", cancellationToken);
    }

    private static async Task SaveAuditAsync(IZauberDbContext dbContext, string description,
        CancellationToken cancellationToken)
    {
        // Inline minimal audit creation to avoid Mediator usage in services
        dbContext.Audits.Add(new ZauberCMS.Core.Audit.Models.Audit { Description = description });
        await dbContext.SaveChangesAsync(cancellationToken);
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

    private IQueryable<Models.Content> ApplyNestedFilter(IQueryable<Models.Content> query,
        BaseQueryContentParameters.NestedContentFilter filter)
    {
        return filter switch
        {
            BaseQueryContentParameters.NestedContentFilter.Exclude => query.Where(c => !c.IsNestedContent),
            BaseQueryContentParameters.NestedContentFilter.Only => query.Where(c => c.IsNestedContent),
            _ => query // Include does nothing
        };
    }

    private async Task ProcessBlockListEditorChangesAsync(Models.Content content, IZauberDbContext dbContext, User user,
        CancellationToken cancellationToken)
    {
        // Load ContentType to identify which properties are BlockListEditor type
        var contentType = await dbContext.ContentTypes
            .AsNoTracking()
            .FirstOrDefaultAsync(ct => ct.Id == content.ContentTypeId, cancellationToken);
            
        if (contentType == null)
            return;

        // Find ContentType properties that are BlockListEditor components
        var blockListPropertyIds = contentType.ContentProperties
            .Where(p => p.ComponentAlias == "ZauberCMS.BlockListEditor")
            .Select(p => p.Id)
            .ToHashSet();

        // Get the actual PropertyData items that match those property types
        var blockListProperties = content.PropertyData
            .Where(p => blockListPropertyIds.Contains(p.ContentTypePropertyId))
            .ToList();

        // Collect all content IDs from all BlockListEditor properties
        var allContentIds = new List<Guid>();
        
        foreach (var property in blockListProperties)
        {
            if (string.IsNullOrWhiteSpace(property.Value))
                continue;

            try
            {
                // Parse the JSON array of content IDs
                var contentIds = JsonSerializer.Deserialize<List<Guid>>(property.Value);
                if (contentIds != null && contentIds.Any())
                {
                    allContentIds.AddRange(contentIds);
                }
            }
            catch (JsonException)
            {
                // Skip invalid JSON - not a breaking error for the main save
                continue;
            }
        }
        
        if (!allContentIds.Any())
            return;
            
        // Batch load all nested content in a single query to prevent N+1
        var nestedContents = await dbContext.Contents
            .Include(c => c.PropertyData)
            .Where(c => allContentIds.Contains(c.Id))
            .ToListAsync(cancellationToken);
        
        // Process each nested content recursively
        foreach (var nestedContent in nestedContents)
        {
            // Recursively save this content, which will process its own BlockListEditor properties
            var nestedSaveParams = new SaveContentParameters
            {
                Content = nestedContent,
                ExcludePropertyData = false,
                UpdateContentRoles = false,
                SaveUnpublishedOnly = false
            };

            await SaveContentAsync(nestedSaveParams, cancellationToken);
        }
    }
}