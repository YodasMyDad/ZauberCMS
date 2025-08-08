using System.Linq.Dynamic.Core;
using System.Security.Cryptography;
using System.Text;
using AutoMapper;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ZauberCMS.Core.Content.Interfaces;
using ZauberCMS.Core.Content.Models;
using ZauberCMS.Core.Content.Parameters;
using ZauberCMS.Core.Data;
using ZauberCMS.Core.Extensions;
using ZauberCMS.Core.Languages.Interfaces;
using ZauberCMS.Core.Plugins;
using ZauberCMS.Core.Settings;
using ZauberCMS.Core.Shared.Models;
using ZauberCMS.Core.Shared.Services;
using ZauberCMS.Core.Membership.Models;
using ZauberCMS.Core.Shared;

namespace ZauberCMS.Core.Content.Services;

public class ContentService(
    IServiceProvider serviceProvider,
    IZauberDbContext dbContext,
    ICacheService cacheService,
    IMapper mapper,
    IOptions<ZauberSettings> settings,
    AuthenticationStateProvider authenticationStateProvider,
    UserManager<User> userManager,
    ExtensionManager extensionManager,
    AppState appState,
    ILanguageService languageService)
    : IContentService
{
    private readonly IServiceProvider _serviceProvider = serviceProvider;

    public async Task<Models.Content?> GetContentAsync(GetContentParameters parameters, CancellationToken cancellationToken = default)
    {
        var cacheKey = GenerateGetContentCacheKey(parameters, dbContext);
        if (parameters.Cached)
        {
            return await cacheService.GetSetCachedItemAsync(cacheKey, async () => await FetchContentAsync(parameters, cancellationToken));
        }
        return await FetchContentAsync(parameters, cancellationToken);
    }

    public async Task<HandlerResult<Models.Content>> SaveContentAsync(SaveContentParameters parameters, CancellationToken cancellationToken = default)
    {
        var authState = await authenticationStateProvider.GetAuthenticationStateAsync();
        var user = await userManager.GetUserAsync(authState.User);
        var isUpdate = true;
        var handlerResult = new HandlerResult<Models.Content>();

        if (parameters.Content == null)
        {
            handlerResult.AddMessage("Content is null", ResultMessageType.Error);
            return handlerResult;
        }

        var unpublishedContent = dbContext.UnpublishedContent.FirstOrDefault(x => x.Id == parameters.Content.UnpublishedContentId);

        if (parameters.SaveUnpublishedOnly)
        {
            var isNew = parameters.Content.UnpublishedContentId == null;
            unpublishedContent ??= new UnpublishedContent();
            mapper.Map(parameters.Content, unpublishedContent.JsonContent);
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

            return await dbContext.SaveChangesAndLog(null, handlerResult, cacheService, extensionManager, cancellationToken);
        }

        if (parameters.Content.Url.IsNullOrWhiteSpace())
        {
            var baseSlug = new SlugHelper().GenerateSlug(parameters.Content.Name);
            parameters.Content.Url = GenerateUniqueUrl(dbContext, baseSlug);
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
            content.LastUpdatedById = user!.Id;
            dbContext.Contents.Add(content);
        }
        else
        {
            mapper.Map(parameters.Content, content);
            content.LastUpdatedById = user!.Id;
            content.DateUpdated = DateTime.UtcNow;

            if (!parameters.ExcludePropertyData)
            {
                UpdateContentPropertyValues(content, parameters.Content.PropertyData);
            }
        }

        if (parameters.UpdateContentRoles)
        {
            UpdateContentRoles(content, parameters);
        }

        if (unpublishedContent != null)
        {
            dbContext.UnpublishedContent.Remove(unpublishedContent);
        }

        content.Path = content.BuildPath(dbContext, isUpdate, settings);

        // Log audit without Mediator
        if (user != null)
        {
            var nameText = content.Name ?? nameof(Models.Content);
            var actionText = isUpdate ? "Updated" : "Created";
            await SaveAuditAsync($"{user.Name} {actionText} {nameText}", cancellationToken);
        }

        return await dbContext.SaveChangesAndLog(content, handlerResult, cacheService, extensionManager, cancellationToken);
    }

    public async Task<PaginatedList<Models.Content>> QueryContentAsync(QueryContentParameters parameters, CancellationToken cancellationToken = default)
    {
        var query = BuildQuery(parameters);
        var cacheKey = query.GenerateCacheKey(typeof(Models.Content));
        if (parameters.Cached)
        {
            return (await cacheService.GetSetCachedItemAsync(cacheKey, async () => await FetchContentAsync(parameters, cancellationToken)))!;
        }
        return await FetchContentAsync(parameters, cancellationToken);
    }

    public async Task<HandlerResult<Models.Content>> DeleteContentAsync(DeleteContentParameters parameters, CancellationToken cancellationToken = default)
    {
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
            await SaveAuditIfUser(loggedInUser, content.Name, "Recycle Binned", cancellationToken);
        }
        else
        {
            var children = dbContext.Contents.AsNoTracking().Where(x => x.ParentId == content.Id);
            if (children.Any())
            {
                handlerResult.AddMessage("Unable to delete content with child content, delete or move those items first", ResultMessageType.Error);
                return handlerResult;
            }

            var propertyDataToDelete = dbContext.ContentPropertyValues.Where(x => x.ContentId == content.Id);
            foreach (var contentPropertyValue in propertyDataToDelete)
            {
                dbContext.ContentPropertyValues.Remove(contentPropertyValue);
            }

            if (content.UnpublishedContentId != null)
            {
                var uContent = dbContext.UnpublishedContent.FirstOrDefault(x => x.Id == content.UnpublishedContentId);
                if (uContent != null) dbContext.UnpublishedContent.Remove(uContent);
            }

            content.PropertyData.Clear();
            await SaveAuditIfUser(loggedInUser, content.Name, "Deleted", cancellationToken);
            dbContext.Contents.Remove(content);
            await appState.NotifyContentDeleted(null, authState.User.Identity?.Name!);
        }

        return await dbContext.SaveChangesAndLog(content, handlerResult, cacheService, extensionManager, cancellationToken);
    }

    public async Task<HandlerResult<Models.Content>> CopyContentAsync(CopyContentParameters parameters, CancellationToken cancellationToken = default)
    {
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
            var parentContent = await dbContext.Contents.AsNoTracking().FirstOrDefaultAsync(x => x.Id == newParentId, cancellationToken);
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
                    copiedDescendant.Path = descendant.Path.Select(id => idMap.TryGetValue(id, out var value) ? value : id).ToList();
                    idMap[descendant.Id] = copiedDescendant.Id;
                    dbContext.Add(copiedDescendant);
                }
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        if (user != null)
        {
            await SaveAuditAsync($"{user.Name} Copied {contentToCopy.Name}", cancellationToken);
        }

        handlerResult.Success = true;
        handlerResult.AddMessage("Content copied successfully.", ResultMessageType.Success);
        return handlerResult;

        Models.Content CreateCopy(Models.Content original, User? currentUser, Guid? parentId = null)
        {
            var copy = mapper.Map<Models.Content>(original);
            copy.Id = Guid.NewGuid();
            copy.Name = original.Name + " (Copy)";
            copy.Url = original.Url + "-copy";
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

    public async Task<EntryModel> GetContentFromRequestAsync(GetContentFromRequestParameters parameters, CancellationToken cancellationToken = default)
    {
        var cacheKey = GenerateGetContentFromRequestCacheKey(parameters);
        return (await cacheService.GetSetCachedItemAsync(cacheKey, async () => await FetchEntryModelAsync(parameters, cancellationToken), 0, 5))!;
    }

    public async Task<ContentType?> GetContentTypeAsync(GetContentTypeParameters parameters, CancellationToken cancellationToken = default)
    {
        return await dbContext.ContentTypes.FirstOrDefaultAsync(x => x.Id == parameters.Id, cancellationToken: cancellationToken);
    }

    public async Task<HandlerResult<ContentType>> SaveContentTypeAsync(SaveContentTypeParameters parameters, CancellationToken cancellationToken = default)
    {
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
                handlerResult.AddMessage("Content Type Alias already exists, change the content type name", ResultMessageType.Error);
                return handlerResult;
            }

            contentType = parameters.ContentType;
            contentType.LastUpdatedById = user!.Id;
            dbContext.ContentTypes.Add(contentType);
        }
        else
        {
            isUpdate = true;
            mapper.Map(parameters.ContentType, contentType);
            contentType.LastUpdatedById = user!.Id;
            contentType.DateUpdated = DateTime.UtcNow;
        }

        if (user != null)
        {
            var actionText = isUpdate ? "Updated" : "Created";
            await SaveAuditAsync($"{user.Name} {actionText} {contentType.Name}", cancellationToken);
        }

        return await dbContext.SaveChangesAndLog(contentType, handlerResult, cacheService, extensionManager, cancellationToken);
    }

    public Task<PaginatedList<ContentType>> QueryContentTypesAsync(QueryContentTypesParameters parameters, CancellationToken cancellationToken = default)
    {
        var query = dbContext.ContentTypes.AsQueryable();

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

        query = parameters.OrderBy switch
        {
            GetContentTypesOrderBy.DateUpdated => query.OrderBy(p => p.DateUpdated),
            GetContentTypesOrderBy.DateUpdatedDescending => query.OrderByDescending(p => p.DateUpdated),
            GetContentTypesOrderBy.DateCreated => query.OrderBy(p => p.DateCreated),
            GetContentTypesOrderBy.DateCreatedDescending => query.OrderByDescending(p => p.DateCreated),
            GetContentTypesOrderBy.Name => query.OrderBy(p => p.Name),
            _ => query.OrderByDescending(p => p.DateUpdated)
        };

        return Task.FromResult(query.ToPaginatedList(parameters.PageIndex, parameters.AmountPerPage));
    }

    public async Task<HandlerResult<ContentType>> DeleteContentTypeAsync(DeleteContentTypeParameters parameters, CancellationToken cancellationToken = default)
    {
        var authState = await authenticationStateProvider.GetAuthenticationStateAsync();
        var user = await userManager.GetUserAsync(authState.User);
        var handlerResult = new HandlerResult<ContentType>();

        var contentUsingContentType = await QueryContentAsync(new QueryContentParameters { ContentTypeId = parameters.ContentTypeId }, cancellationToken);
        if (contentUsingContentType.Items.Any())
        {
            handlerResult.Success = false;
            handlerResult.AddMessage("Unable to delete, because this ContentType is being used", ResultMessageType.Warning);
            return handlerResult;
        }

        var children = await QueryContentTypesAsync(new QueryContentTypesParameters { ParentId = parameters.ContentTypeId }, cancellationToken);
        if (children.Items.Any())
        {
            handlerResult.Success = false;
            handlerResult.AddMessage("Unable to delete, because this ContentType has children", ResultMessageType.Warning);
            return handlerResult;
        }

        var contentType = dbContext.ContentTypes.FirstOrDefault(x => x.Id == parameters.ContentTypeId);
        if (contentType != null)
        {
            if (contentType.IsComposition)
            {
                var anyUsingThisComposition = QueryContentTypesForComposition(parameters.ContentTypeId);
                if (anyUsingThisComposition.Items.Any())
                {
                    handlerResult.Success = false;
                    handlerResult.AddMessage("Unable to delete, because there are content types using this composition, remove it first", ResultMessageType.Warning);
                    return handlerResult;
                }
            }

            if (user != null)
            {
                await SaveAuditAsync($"{user.Name} Deleted {contentType.Name}", cancellationToken);
            }

            dbContext.ContentTypes.Remove(contentType);
            return await dbContext.SaveChangesAndLog(contentType, handlerResult, cacheService, extensionManager, cancellationToken);
        }

        handlerResult.AddMessage("Unable to delete, as no ContentType with that id exists", ResultMessageType.Warning);
        return handlerResult;
    }

    public async Task<Domain> GetDomainAsync(GetDomainParameters parameters, CancellationToken cancellationToken = default)
    {
        var query = dbContext.Domains.AsQueryable();
        if (parameters.AsNoTracking)
        {
            query = query.AsNoTracking();
        }

        if (parameters.Url != null)
        {
            return await query.FirstOrDefaultAsync(x => x.Url == parameters.Url, cancellationToken: cancellationToken) ?? new Domain();
        }

        if (parameters.Id != null)
        {
            return await query.FirstOrDefaultAsync(x => x.Id == parameters.Id, cancellationToken: cancellationToken) ?? new Domain();
        }

        return await query.FirstOrDefaultAsync(cancellationToken: cancellationToken) ?? new Domain();
    }

    public async Task<HandlerResult<Domain>> SaveDomainAsync(SaveDomainParameters parameters, CancellationToken cancellationToken = default)
    {
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
            mapper.Map(parameters.Domain, domain);
            domain.DateUpdated = DateTime.UtcNow;
        }

        if (user != null)
        {
            var actionText = isUpdate ? "Updated" : "Created";
            await SaveAuditAsync($"{user.Name} {actionText} Domain ({domain.Url})", cancellationToken);
        }

        return await dbContext.SaveChangesAndLog(domain, handlerResult, cacheService, extensionManager, cancellationToken);
    }

    public Task<PaginatedList<Domain>> QueryDomainAsync(QueryDomainParameters parameters, CancellationToken cancellationToken = default)
    {
        var query = dbContext.Domains.AsQueryable();
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

        return Task.FromResult(query.ToPaginatedList(parameters.PageIndex, parameters.AmountPerPage));
    }

    public async Task<HandlerResult<Domain?>> DeleteDomainAsync(DeleteDomainParameters parameters, CancellationToken cancellationToken = default)
    {
        var authState = await authenticationStateProvider.GetAuthenticationStateAsync();
        var user = await userManager.GetUserAsync(authState.User);
        var handlerResult = new HandlerResult<Domain?>();

        Domain? domain = null;
        if (parameters.Id != null)
        {
            domain = await dbContext.Domains.FirstOrDefaultAsync(x => x.Id == parameters.Id, cancellationToken: cancellationToken);
        }
        else if (parameters.ContentId != null)
        {
            domain = await dbContext.Domains.FirstOrDefaultAsync(x => x.ContentId == parameters.ContentId, cancellationToken: cancellationToken);
        }

        if (domain != null)
        {
            if (user != null)
            {
                await SaveAuditAsync($"{user.Name} Deleted Domain ({domain.Url})", cancellationToken);
            }
            dbContext.Domains.Remove(domain);
            return await dbContext.SaveChangesAndLog(domain, handlerResult, cacheService, extensionManager, cancellationToken);
        }

        handlerResult.AddMessage("Unable to delete, as no Domain with that id exists", ResultMessageType.Warning);
        return handlerResult;
    }

    public async Task<bool> AnyContentAsync(AnyContentParameters parameters, CancellationToken cancellationToken = default)
    {
        return await dbContext.Contents.AsNoTracking().AnyAsync(cancellationToken: cancellationToken);
    }

    public async Task<bool> HasChildContentAsync(HasChildContentParameters parameters, CancellationToken cancellationToken = default)
    {
        var cacheKey = GenerateHasChildContentCacheKey(parameters);
        if (parameters.Cached)
        {
            return await cacheService.GetSetCachedItemAsync(cacheKey, async () => await dbContext.Contents.AsNoTracking().AnyAsync(c => c.ParentId == parameters.ParentId, cancellationToken: cancellationToken));
        }
        return await dbContext.Contents.AsNoTracking().AnyAsync(c => c.ParentId == parameters.ParentId, cancellationToken: cancellationToken);
    }

    public async Task<bool> HasChildContentTypeAsync(HasChildContentTypeParameters parameters, CancellationToken cancellationToken = default)
    {
        var cacheKey = GenerateHasChildContentTypeCacheKey(parameters);
        if (parameters.Cached)
        {
            return await cacheService.GetSetCachedItemAsync(cacheKey, async () => await dbContext.ContentTypes.AsNoTracking().AnyAsync(c => c.ParentId == parameters.ParentId, cancellationToken: cancellationToken));
        }
        return await dbContext.ContentTypes.AsNoTracking().AnyAsync(c => c.ParentId == parameters.ParentId, cancellationToken: cancellationToken);
    }

    public async Task<Dictionary<object, string>> GetContentLanguagesAsync(GetContentLanguagesParameters parameters, CancellationToken cancellationToken = default)
    {
        var query = dbContext.Contents.AsNoTracking()
            .Include(x => x.Language)
            .Select(c => new { c.Id, c.Url, c.Language })
            .Where(x => x.Language != null && x.Url != null);

        var queryString = query.ToQueryString();
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(queryString));
        var cacheKey = typeof(Models.Content).ToCacheKey(Convert.ToBase64String(hash));

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

    public async Task<List<Domain>> GetCachedDomainsAsync(CachedDomainsParameters parameters, CancellationToken cancellationToken = default)
    {
        var query = dbContext.Domains.AsNoTracking().Include(x => x.Language);
        var queryString = query.ToQueryString();
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(queryString));
        var cacheKey = typeof(Domain).ToCacheKey(Convert.ToBase64String(hash));
        return (await cacheService.GetSetCachedItemAsync(cacheKey, async () => await query.ToListAsync(cancellationToken: cancellationToken)))!;
    }

    public async Task<HandlerResult<UnpublishedContent>> ClearUnpublishedContentAsync(ClearUnpublishedContentParameters parameters, CancellationToken cancellationToken = default)
    {
        var handlerResult = new HandlerResult<UnpublishedContent>();
        var content = await dbContext.Contents.FirstOrDefaultAsync(x => x.Id == parameters.ContentId, cancellationToken: cancellationToken);
        if (content?.UnpublishedContentId != null)
        {
            var uContent = await dbContext.UnpublishedContent.FirstOrDefaultAsync(x => x.Id == content.UnpublishedContentId, cancellationToken: cancellationToken);
            if (uContent != null) dbContext.UnpublishedContent.Remove(uContent);
            var result = (await dbContext.SaveChangesAndLog(uContent, handlerResult, cacheService, extensionManager, cancellationToken))!;
            return result;
        }
        return handlerResult;
    }

    public async Task<DataGridResult<Models.Content>> GetDataGridContentAsync(DataGridContentParameters parameters, CancellationToken cancellationToken = default)
    {
        var result = new DataGridResult<Models.Content>();
        var query = dbContext.Contents
            .Include(x => x.ContentType)
            .Include(x => x.LastUpdatedBy)
            .Where(x => x.Deleted == false)
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
            var contentType = dbContext.ContentTypes.AsNoTracking().FirstOrDefault(x => x.Alias == parameters.ContentTypeAlias);
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

        result.Count = query.Count();
        result.Items = await query.Skip(parameters.Skip).Take(parameters.Take).ToListAsync(cancellationToken: cancellationToken);
        return result;
    }

    // Helpers
    private static string GenerateGetContentCacheKey(GetContentParameters request, IZauberDbContext dbContext)
    {
        var query = BuildQuery(request, dbContext);
        var queryString = query.ToQueryString();
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(queryString));
        return typeof(Models.Content).ToCacheKey(Convert.ToBase64String(hash));
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
            query = request.IncludeUnpublished ? query.Include(x => x.Children)
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

    private IQueryable<Models.Content> BuildQuery(QueryContentParameters request)
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
                query = !request.IncludeUnpublished ? query.Where(x => x.Published) : query.Include(x => x.UnpublishedContent);
            }

            if (request.IsDeleted != null)
            {
                query = query.Where(x => x.Deleted == request.IsDeleted);
            }

            if (request.IncludeChildren)
            {
                query = request.IncludeUnpublished ? query.Include(x => x.Children)
                        .ThenInclude(x => x.UnpublishedContent)
                    : query.Include(x => x.Children.Where(c => c.Published));
            }

            if (request.RootContentOnly)
            {
                query = query.Where(x => x.ParentId == null);
            }

            if (request.TagSlugs.Any())
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
                var contentType = dbContext.ContentTypes.AsNoTracking().FirstOrDefault(x => x.Alias == request.ContentTypeAlias);
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

    private Task<PaginatedList<Models.Content>> FetchContentAsync(QueryContentParameters request, CancellationToken cancellationToken)
    {
        var query = BuildQuery(request);
        return Task.FromResult(query.ToPaginatedList(request.PageIndex, request.AmountPerPage));
    }

    private async Task<Models.Content?> FetchContentAsync(GetContentParameters request, CancellationToken cancellationToken)
    {
        var query = BuildQuery(request, dbContext);
        return await query.FirstOrDefaultAsync(cancellationToken: cancellationToken);
    }

    private async Task<EntryModel> FetchEntryModelAsync(GetContentFromRequestParameters request, CancellationToken cancellationToken)
    {
        var entryModel = new EntryModel();
        var contentQueryable = dbContext.Contents.AsNoTracking().Include(x => x.ContentType);

        var domains = await GetCachedDomainsAsync(new CachedDomainsParameters(), cancellationToken);
        var contentWithLanguages = await GetContentLanguagesAsync(new GetContentLanguagesParameters(), cancellationToken);

        var matchedDomain = MatchDomainWithContent(request.Url ?? string.Empty, domains);

        var content = request.IsRootContent
            ? matchedDomain != null
                ? await contentQueryable.Select(c => new { c.Id, c.InternalRedirectId, c.ContentType!.IncludeChildren, c.Path })
                    .FirstOrDefaultAsync(x => x.Id == matchedDomain.ContentId, cancellationToken)
                : await contentQueryable.Where(c => c.IsRootContent && c.Published)
                    .Select(c => new { c.Id, c.InternalRedirectId, c.ContentType!.IncludeChildren, c.Path })
                    .FirstOrDefaultAsync(cancellationToken)
            : await contentQueryable.Where(c => c.Url == request.Slug && c.Published)
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

        var fullContent = await query.FirstOrDefaultAsync(c => c.Id == content.Id, cancellationToken: cancellationToken);
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
        var allLanguageData = await languageService.GetCachedAllLanguageDictionariesAsync(new ZauberCMS.Core.Languages.Parameters.GetCachedAllLanguageDictionariesParameters(), cancellationToken);
        if (allLanguageData.TryGetValue(languageIsoCode, out var lng) && lng != null)
        {
            entryModel.LanguageKeys = lng;
        }

        return entryModel;
    }

    private static string GenerateGetContentFromRequestCacheKey(GetContentFromRequestParameters request)
    {
        var keyBuilder = new StringBuilder();
        keyBuilder.Append($"GetContentFromRequest-");
        keyBuilder.Append($"Url:{request.Url ?? "null"}-");
        keyBuilder.Append($"Slug:{request.Slug ?? "null"}-");
        keyBuilder.Append($"IsRoot:{request.IsRootContent}-");
        keyBuilder.Append($"IncludeChildren:{request.IncludeChildren}");
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(keyBuilder.ToString()));
        return typeof(Models.Content).ToCacheKey(Convert.ToBase64String(hash));
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

    private static string GenerateHasChildContentCacheKey(HasChildContentParameters request)
    {
        var key = $"HasChild-{request.ParentId}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(key));
        return typeof(Models.Content).ToCacheKey(Convert.ToBase64String(hash));
    }

    private static string GenerateHasChildContentTypeCacheKey(HasChildContentTypeParameters request)
    {
        var key = $"HasContentTypeChild-{request.ParentId}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(key));
        return typeof(ContentType).ToCacheKey(Convert.ToBase64String(hash));
    }

    private static string GenerateUniqueUrl(IZauberDbContext dbContext, string baseSlug)
    {
        var url = baseSlug;
        if (!dbContext.Contents.Any(c => c.Url == url))
        {
            return url;
        }

        var counter = 1;
        while (dbContext.Contents.Any(c => c.Url == url))
        {
            url = $"{baseSlug}-{counter}";
            counter++;
        }
        return url;
    }

    private void UpdateContentRoles(Models.Content content, SaveContentParameters request)
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

    private void UpdateContentPropertyValues(Models.Content content, List<ContentPropertyValue> newPropertyValues)
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
                mapper.Map(newPropertyValue, existingPropertyValue);
            }
        }
    }

    private PaginatedList<ContentType> QueryContentTypesForComposition(Guid compositionId)
    {
        var query = dbContext.ContentTypes.WhereHasCompositionsUsing(compositionId);
        return query.ToPaginatedList(1, int.MaxValue);
    }

    private async Task SaveAuditIfUser(User? user, string? name, string action, CancellationToken cancellationToken)
    {
        if (user == null) return;
        await SaveAuditAsync($"{user.Name} {action} {name ?? string.Empty}", cancellationToken);
    }

    private async Task SaveAuditAsync(string description, CancellationToken cancellationToken)
    {
        // Inline minimal audit creation to avoid Mediator usage in services
        dbContext.Audits.Add(new ZauberCMS.Core.Audit.Models.Audit { Description = description });
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}