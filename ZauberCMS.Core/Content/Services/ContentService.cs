using System.Security.Cryptography;
using System.Text;
using AutoMapper;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ZauberCMS.Core.Content.Interfaces;
using ZauberCMS.Core.Content.Models;
using ZauberCMS.Core.Content.Parameters;
using ZauberCMS.Core.Data;
using ZauberCMS.Core.Extensions;
using ZauberCMS.Core.Membership.Models;
using ZauberCMS.Core.Plugins;
using ZauberCMS.Core.Settings;
using ZauberCMS.Core.Shared.Models;
using ZauberCMS.Core.Shared.Services;

namespace ZauberCMS.Core.Content.Services;

public class ContentService(
    IServiceProvider serviceProvider,
    ICacheService cacheService,
    IMapper mapper,
    IOptions<ZauberSettings> settings,
    AuthenticationStateProvider authenticationStateProvider,
    ExtensionManager extensionManager) : IContentService
{
    private readonly SlugHelper _slugHelper = new();

    public async Task<Content?> GetContentAsync(GetContentParameters parameters, CancellationToken cancellationToken = default)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IZauberDbContext>();
        var cacheKey = GenerateCacheKey(parameters, dbContext);

        if (parameters.Cached)
        {
            return await cacheService.GetSetCachedItemAsync(cacheKey, async () => await FetchContentAsync(parameters, dbContext, cancellationToken));
        }

        return await FetchContentAsync(parameters, dbContext, cancellationToken);
    }

    public async Task<HandlerResult<Content>> SaveContentAsync(SaveContentParameters parameters, CancellationToken cancellationToken = default)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IZauberDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var authState = await authenticationStateProvider.GetAuthenticationStateAsync();
        var user = await userManager.GetUserAsync(authState.User);
        var isUpdate = true;
        var handlerResult = new HandlerResult<Content>();

        if (parameters.Content != null)
        {
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
                var baseSlug = _slugHelper.GenerateSlug(parameters.Content.Name);
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
            
            // TODO: Add audit logging - need to inject IAuditService and create AddAudit extension that uses it
            return await dbContext.SaveChangesAndLog(content, handlerResult, cacheService, extensionManager, cancellationToken);
        }

        handlerResult.AddMessage("Content is null", ResultMessageType.Error);
        return handlerResult;
    }

    public async Task<PaginatedList<Content>> QueryContentAsync(QueryContentParameters parameters, CancellationToken cancellationToken = default)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IZauberDbContext>();
        
        var query = dbContext.Contents
            .Include(x => x.ContentType)
            .Include(x => x.PropertyData)
            .AsSplitQuery()
            .AsQueryable();

        if (parameters.AsNoTracking)
        {
            query = query.AsNoTracking();
        }

        if (!parameters.IncludeUnpublished)
        {
            query = query.Where(x => x.Published);
        }

        if (parameters.IncludeUnpublishedContent)
        {
            query = query.Include(x => x.UnpublishedContent);
        }

        if (parameters.IncludeParent)
        {
            query = query.Include(x => x.Parent);
        }

        if (parameters.IncludeChildren)
        {
            query = parameters.IncludeUnpublished ? query.Include(x => x.Children) 
                : query.Include(x => x.Children.Where(c => c.Published));
            query = query.AsSplitQuery();
        }

        if (parameters.IncludeContentRoles)
        {
            query = query.Include(x => x.ContentRoles).ThenInclude(x => x.Role);
            query = query.AsSplitQuery();
        }

        if (!parameters.ContentTypeAlias.IsNullOrWhiteSpace())
        {
            query = query.Where(x => x.ContentType != null && x.ContentType.Alias == parameters.ContentTypeAlias);
        }

        if (parameters.ContentTypeAliases?.Any() == true)
        {
            query = query.Where(x => x.ContentType != null && parameters.ContentTypeAliases.Contains(x.ContentType.Alias));
        }

        if (parameters.ParentId.HasValue)
        {
            query = query.Where(x => x.ParentId == parameters.ParentId);
        }

        if (parameters.DomainId.HasValue)
        {
            query = query.Where(x => x.DomainId == parameters.DomainId);
        }

        if (!parameters.LanguageCode.IsNullOrWhiteSpace())
        {
            query = query.Where(x => x.LanguageCode == parameters.LanguageCode);
        }

        if (!parameters.SearchTerm.IsNullOrWhiteSpace())
        {
            if (parameters.SearchFields?.Any() == true)
            {
                var searchPredicate = parameters.SearchFields.Aggregate<string, IQueryable<Content>>(null, (current, field) =>
                {
                    var fieldQuery = query.Where(x => EF.Property<string>(x, field).Contains(parameters.SearchTerm));
                    return current == null ? fieldQuery : current.Union(fieldQuery);
                });
                if (searchPredicate != null)
                {
                    query = searchPredicate;
                }
            }
            else
            {
                query = query.Where(x => x.Name.Contains(parameters.SearchTerm) || 
                                         x.Url.Contains(parameters.SearchTerm));
            }
        }

        if (!parameters.Tag.IsNullOrWhiteSpace())
        {
            query = query.Where(x => x.Tags.Any(t => t.Tag.Name == parameters.Tag));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        if (!parameters.OrderBy.IsNullOrWhiteSpace())
        {
            query = query.OrderBy(parameters.OrderBy);
        }
        else
        {
            query = query.OrderBy(x => x.SortOrder).ThenBy(x => x.Name);
        }

        if (parameters.AmountPerPage > 0)
        {
            query = query.Skip(parameters.PageIndex * parameters.AmountPerPage).Take(parameters.AmountPerPage);
        }

        return query.ToPaginatedList(parameters.PageIndex, parameters.AmountPerPage);
    }

    public async Task<HandlerResult<Content>> DeleteContentAsync(DeleteContentParameters parameters, CancellationToken cancellationToken = default)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IZauberDbContext>();
        var handlerResult = new HandlerResult<Content>();

        var content = await dbContext.Contents
            .Include(x => x.Children)
            .FirstOrDefaultAsync(x => x.Id == parameters.Id, cancellationToken);

        if (content == null)
        {
            handlerResult.AddMessage("Content not found", ResultMessageType.Error);
            return handlerResult;
        }

        if (content.Children.Any())
        {
            handlerResult.AddMessage("Cannot delete content that has children", ResultMessageType.Error);
            return handlerResult;
        }

        dbContext.Contents.Remove(content);
        return await dbContext.SaveChangesAndLog(content, handlerResult, cacheService, extensionManager, cancellationToken);
    }

    public async Task<HandlerResult<Content>> CopyContentAsync(CopyContentParameters parameters, CancellationToken cancellationToken = default)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IZauberDbContext>();
        var handlerResult = new HandlerResult<Content>();

        var sourceContent = await dbContext.Contents
            .Include(x => x.PropertyData)
            .Include(x => x.Children)
            .FirstOrDefaultAsync(x => x.Id == parameters.ContentId, cancellationToken);

        if (sourceContent == null)
        {
            handlerResult.AddMessage("Source content not found", ResultMessageType.Error);
            return handlerResult;
        }

        var copiedContent = mapper.Map<Content>(sourceContent);
        copiedContent.Id = Guid.NewGuid();
        copiedContent.ParentId = parameters.ParentId;
        copiedContent.Name = $"{sourceContent.Name} (Copy)";
        copiedContent.Url = GenerateUniqueUrl(dbContext, _slugHelper.GenerateSlug(copiedContent.Name));
        copiedContent.DateCreated = DateTime.UtcNow;
        copiedContent.DateUpdated = DateTime.UtcNow;

        dbContext.Contents.Add(copiedContent);

        if (parameters.IncludeChildren && sourceContent.Children.Any())
        {
            foreach (var child in sourceContent.Children)
            {
                var copyChildResult = await CopyContentAsync(new CopyContentParameters 
                { 
                    ContentId = child.Id, 
                    ParentId = copiedContent.Id, 
                    IncludeChildren = true 
                }, cancellationToken);
                
                if (!copyChildResult.Success)
                {
                    handlerResult.Messages.AddRange(copyChildResult.Messages);
                    return handlerResult;
                }
            }
        }

        return await dbContext.SaveChangesAndLog(copiedContent, handlerResult, cacheService, extensionManager, cancellationToken);
    }

    public async Task<EntryModel> GetContentFromRequestAsync(GetContentFromRequestParameters parameters, CancellationToken cancellationToken = default)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IZauberDbContext>();

        var query = dbContext.Contents
            .Include(x => x.ContentType)
            .Include(x => x.PropertyData)
            .AsSplitQuery()
            .AsQueryable();

        if (parameters.AsNoTracking)
        {
            query = query.AsNoTracking();
        }

        if (!parameters.IncludeUnpublished)
        {
            query = query.Where(x => x.Published);
        }

        if (parameters.IncludeUnpublishedContent)
        {
            query = query.Include(x => x.UnpublishedContent);
        }

        if (parameters.IncludeParent)
        {
            query = query.Include(x => x.Parent);
        }

        if (parameters.IncludeChildren)
        {
            query = parameters.IncludeUnpublished ? query.Include(x => x.Children) 
                : query.Include(x => x.Children.Where(c => c.Published));
            query = query.AsSplitQuery();
        }

        if (parameters.IncludeContentRoles)
        {
            query = query.Include(x => x.ContentRoles).ThenInclude(x => x.Role);
            query = query.AsSplitQuery();
        }

        query = query.Where(x => x.Url == parameters.Url);

        if (parameters.DomainId.HasValue)
        {
            query = query.Where(x => x.DomainId == parameters.DomainId);
        }

        if (!parameters.LanguageCode.IsNullOrWhiteSpace())
        {
            query = query.Where(x => x.LanguageCode == parameters.LanguageCode);
        }

        return await query.FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<ContentType?> GetContentTypeAsync(GetContentTypeParameters parameters, CancellationToken cancellationToken = default)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IZauberDbContext>();

        var query = dbContext.ContentTypes.AsQueryable();

        if (parameters.Id.HasValue)
        {
            return await query.FirstOrDefaultAsync(x => x.Id == parameters.Id, cancellationToken);
        }

        if (!parameters.Alias.IsNullOrWhiteSpace())
        {
            return await query.FirstOrDefaultAsync(x => x.Alias == parameters.Alias, cancellationToken);
        }

        return null;
    }

    public async Task<HandlerResult<ContentType>> SaveContentTypeAsync(SaveContentTypeParameters parameters, CancellationToken cancellationToken = default)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IZauberDbContext>();
        var handlerResult = new HandlerResult<ContentType>();

        if (parameters.ContentType == null)
        {
            handlerResult.AddMessage("ContentType is null", ResultMessageType.Error);
            return handlerResult;
        }

        var existingContentType = await dbContext.ContentTypes
            .FirstOrDefaultAsync(x => x.Id == parameters.ContentType.Id, cancellationToken);

        if (existingContentType == null)
        {
            dbContext.ContentTypes.Add(parameters.ContentType);
        }
        else
        {
            mapper.Map(parameters.ContentType, existingContentType);
        }

        return await dbContext.SaveChangesAndLog(parameters.ContentType, handlerResult, cacheService, extensionManager, cancellationToken);
    }

    public async Task<PaginatedList<ContentType>> QueryContentTypesAsync(QueryContentTypesParameters parameters, CancellationToken cancellationToken = default)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IZauberDbContext>();

        var query = dbContext.ContentTypes.AsQueryable();

        if (parameters.AsNoTracking)
        {
            query = query.AsNoTracking();
        }

        if (!parameters.SearchTerm.IsNullOrWhiteSpace())
        {
            query = query.Where(x => x.Name.Contains(parameters.SearchTerm) || x.Alias.Contains(parameters.SearchTerm));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        if (!parameters.OrderBy.IsNullOrWhiteSpace())
        {
            query = query.OrderBy(parameters.OrderBy);
        }
        else
        {
            query = query.OrderBy(x => x.Name);
        }

        if (parameters.AmountPerPage > 0)
        {
            query = query.Skip(parameters.PageIndex * parameters.AmountPerPage).Take(parameters.AmountPerPage);
        }

        var items = await query.ToListAsync(cancellationToken);

        return new HandlerResult<ContentType>
        {
            Success = true,
            Items = items,
            TotalCount = totalCount
        };
    }

    public async Task<HandlerResult<ContentType>> DeleteContentTypeAsync(DeleteContentTypeParameters parameters, CancellationToken cancellationToken = default)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IZauberDbContext>();
        var handlerResult = new HandlerResult<ContentType>();

        var contentType = await dbContext.ContentTypes
            .FirstOrDefaultAsync(x => x.Id == parameters.Id, cancellationToken);

        if (contentType == null)
        {
            handlerResult.AddMessage("ContentType not found", ResultMessageType.Error);
            return handlerResult;
        }

        var hasContent = await dbContext.Contents.AnyAsync(x => x.ContentTypeId == parameters.Id, cancellationToken);
        if (hasContent)
        {
            handlerResult.AddMessage("Cannot delete content type that has content", ResultMessageType.Error);
            return handlerResult;
        }

        dbContext.ContentTypes.Remove(contentType);
        return await dbContext.SaveChangesAndLog(contentType, handlerResult, cacheService, extensionManager, cancellationToken);
    }

    public async Task<Domain?> GetDomainAsync(GetDomainParameters parameters, CancellationToken cancellationToken = default)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IZauberDbContext>();

        var query = dbContext.Domains.AsQueryable();

        if (parameters.Id.HasValue)
        {
            return await query.FirstOrDefaultAsync(x => x.Id == parameters.Id, cancellationToken);
        }

        if (!parameters.DomainName.IsNullOrWhiteSpace())
        {
            return await query.FirstOrDefaultAsync(x => x.DomainName == parameters.DomainName, cancellationToken);
        }

        return null;
    }

    public async Task<HandlerResult<Domain>> SaveDomainAsync(SaveDomainParameters parameters, CancellationToken cancellationToken = default)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IZauberDbContext>();
        var handlerResult = new HandlerResult<Domain>();

        if (parameters.Domain == null)
        {
            handlerResult.AddMessage("Domain is null", ResultMessageType.Error);
            return handlerResult;
        }

        var existingDomain = await dbContext.Domains
            .FirstOrDefaultAsync(x => x.Id == parameters.Domain.Id, cancellationToken);

        if (existingDomain == null)
        {
            dbContext.Domains.Add(parameters.Domain);
        }
        else
        {
            mapper.Map(parameters.Domain, existingDomain);
        }

        return await dbContext.SaveChangesAndLog(parameters.Domain, handlerResult, cacheService, extensionManager, cancellationToken);
    }

    public async Task<PaginatedList<Domain>> QueryDomainAsync(QueryDomainParameters parameters, CancellationToken cancellationToken = default)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IZauberDbContext>();

        var query = dbContext.Domains.AsQueryable();

        if (parameters.AsNoTracking)
        {
            query = query.AsNoTracking();
        }

        var totalCount = await query.CountAsync(cancellationToken);

        if (!parameters.OrderBy.IsNullOrWhiteSpace())
        {
            query = query.OrderBy(parameters.OrderBy);
        }
        else
        {
            query = query.OrderBy(x => x.DomainName);
        }

        if (parameters.AmountPerPage > 0)
        {
            query = query.Skip(parameters.PageIndex * parameters.AmountPerPage).Take(parameters.AmountPerPage);
        }

        var items = await query.ToListAsync(cancellationToken);

        return new HandlerResult<Domain>
        {
            Success = true,
            Items = items,
            TotalCount = totalCount
        };
    }

    public async Task<HandlerResult<Domain>> DeleteDomainAsync(DeleteDomainParameters parameters, CancellationToken cancellationToken = default)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IZauberDbContext>();
        var handlerResult = new HandlerResult<Domain>();

        var domain = await dbContext.Domains
            .FirstOrDefaultAsync(x => x.Id == parameters.Id, cancellationToken);

        if (domain == null)
        {
            handlerResult.AddMessage("Domain not found", ResultMessageType.Error);
            return handlerResult;
        }

        var hasContent = await dbContext.Contents.AnyAsync(x => x.DomainId == parameters.Id, cancellationToken);
        if (hasContent)
        {
            handlerResult.AddMessage("Cannot delete domain that has content", ResultMessageType.Error);
            return handlerResult;
        }

        dbContext.Domains.Remove(domain);
        return await dbContext.SaveChangesAndLog(domain, handlerResult, cacheService, extensionManager, cancellationToken);
    }

    public async Task<bool> AnyContentAsync(AnyContentParameters parameters, CancellationToken cancellationToken = default)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IZauberDbContext>();

        var query = dbContext.Contents.AsQueryable();

        if (!parameters.ContentTypeAlias.IsNullOrWhiteSpace())
        {
            query = query.Where(x => x.ContentType != null && x.ContentType.Alias == parameters.ContentTypeAlias);
        }

        return await query.AnyAsync(cancellationToken);
    }

    public async Task<bool> HasChildContentAsync(HasChildContentParameters parameters, CancellationToken cancellationToken = default)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IZauberDbContext>();

        return await dbContext.Contents.AnyAsync(x => x.ParentId == parameters.Id, cancellationToken);
    }

    public async Task<bool> HasChildContentTypeAsync(HasChildContentTypeParameters parameters, CancellationToken cancellationToken = default)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IZauberDbContext>();

        return await dbContext.ContentTypes.AnyAsync(x => x.ParentId == parameters.Id, cancellationToken);
    }

    public async Task<Dictionary<object, string>> GetContentLanguagesAsync(GetContentLanguagesParameters parameters, CancellationToken cancellationToken = default)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IZauberDbContext>();
        
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

            // Set the Urls first
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
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IZauberDbContext>();
        var cacheKey = $"domains_{parameters.LanguageCode}";

        return await cacheService.GetSetCachedItemAsync(cacheKey, async () =>
        {
            var query = dbContext.Domains.AsNoTracking();
            
            if (!parameters.LanguageCode.IsNullOrWhiteSpace())
            {
                query = query.Where(x => x.LanguageCode == parameters.LanguageCode);
            }

            return await query.ToListAsync(cancellationToken);
        });
    }

    public async Task<HandlerResult<Content>> ClearUnpublishedContentAsync(ClearUnpublishedContentParameters parameters, CancellationToken cancellationToken = default)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IZauberDbContext>();
        var handlerResult = new HandlerResult<Content>();

        var unpublishedContent = await dbContext.UnpublishedContent
            .FirstOrDefaultAsync(x => x.Id == parameters.ContentId, cancellationToken);

        if (unpublishedContent != null)
        {
            dbContext.UnpublishedContent.Remove(unpublishedContent);
        }

        var content = await dbContext.Contents
            .FirstOrDefaultAsync(x => x.UnpublishedContentId == parameters.ContentId, cancellationToken);

        if (content != null)
        {
            content.UnpublishedContentId = null;
        }

        return await dbContext.SaveChangesAndLog(content, handlerResult, cacheService, extensionManager, cancellationToken);
    }

    public async Task<DataGridResult<Content>> GetDataGridContentAsync(DataGridContentParameters parameters, CancellationToken cancellationToken = default)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IZauberDbContext>();
        
        var result = new DataGridResult<Content>();

        var query = dbContext.Contents.Include(x => x.ContentType)
            .Include(x => x.PropertyData).AsSplitQuery().AsQueryable();

        if (parameters.AsNoTracking)
        {
            query = query.AsNoTracking();
        }

        if (!parameters.IncludeUnpublished)
        {
            query = query.Where(x => x.Published);
        }

        if (!parameters.ContentTypeAlias.IsNullOrWhiteSpace())
        {
            query = query.Where(x => x.ContentType != null && x.ContentType.Alias == parameters.ContentTypeAlias);
        }

        if (parameters.ContentTypeId.HasValue)
        {
            query = query.Where(x => x.ContentTypeId == parameters.ContentTypeId);
        }

        if (parameters.ParentId.HasValue)
        {
            query = query.Where(x => x.ParentId == parameters.ParentId);
        }

        if (parameters.LastEditedBy.HasValue)
        {
            query = query.Where(x => x.LastEditedBy == parameters.LastEditedBy);
        }

        if (!parameters.Filter.IsNullOrWhiteSpace())
        {
            query = query.Where(parameters.Filter);
        }

        if (!string.IsNullOrEmpty(parameters.Order))
        {
            // Sort via the OrderBy method
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

        // Important!!! Make sure the Count property of RadzenDataGrid is set.
        result.Count = query.Count();

        // Perform paging via Skip and Take.
        result.Items = await query.Skip(parameters.Skip).Take(parameters.Take).ToListAsync(cancellationToken: cancellationToken);

        return result;
    }

    private static string GenerateCacheKey(GetContentParameters parameters, IZauberDbContext dbContext)
    {
        var query = BuildQuery(parameters, dbContext);
        var queryString = query.ToQueryString();
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(queryString));
        return typeof(Content).ToCacheKey(Convert.ToBase64String(hash));
    }

    private static IQueryable<Content> BuildQuery(GetContentParameters parameters, IZauberDbContext dbContext)
    {
        var query = dbContext.Contents
            .Include(x => x.ContentType)
            .Include(x => x.PropertyData)
            .AsSplitQuery()
            .AsQueryable();
        
        if (parameters.AsNoTracking)
        {
            query = query.AsNoTracking();
        }
        
        if (!parameters.IncludeUnpublished)
        {
            query = query.Where(x => x.Published);
        }
        
        if (parameters.IncludeUnpublishedContent)
        {
            query = query.Include(x => x.UnpublishedContent);
        }
        
        if (parameters.IncludeParent)
        {
            query = query.Include(x => x.Parent);
        }
        
        if (parameters.IncludeChildren)
        {
            query = parameters.IncludeUnpublished ? query.Include(x => x.Children) 
                : query.Include(x => x.Children.Where(c => c.Published));
            query = query.AsSplitQuery();
        }

        if (parameters.IncludeContentRoles)
        {
            query = query.Include(x => x.ContentRoles).ThenInclude(x => x.Role);
            query = query.AsSplitQuery();
        }

        if (parameters.Id != null)
        {
            return query.Where(x => x.Id == parameters.Id);
        }
        
        if (!parameters.ContentTypeAlias.IsNullOrWhiteSpace())
        {
            return query.Where(x => x.ContentType != null && x.ContentType.Alias == parameters.ContentTypeAlias);
        }

        return query;
    }

    private static async Task<Content?> FetchContentAsync(GetContentParameters parameters, IZauberDbContext dbContext, CancellationToken cancellationToken)
    {
        var query = BuildQuery(parameters, dbContext);
        return await query.FirstOrDefaultAsync(cancellationToken: cancellationToken);
    }

    private void UpdateContentRoles(IZauberDbContext dbContext, Content content, SaveContentParameters parameters)
    {
        var existingRoles = dbContext.ContentRoles
            .Where(r => r.ContentId == content.Id)
            .ToList();

        var rolesToRemove = existingRoles
            .Where(er => parameters.Roles.All(rr => rr.Id != er.RoleId))
            .ToList();

        if (rolesToRemove.Count != 0)
        {
            dbContext.ContentRoles.RemoveRange(rolesToRemove);
        }

        var rolesToAdd = parameters.Roles
            .Where(rr => existingRoles.All(er => er.RoleId != rr.Id))
            .ToList();

        if (rolesToAdd.Count != 0)
        {
            foreach (var role in rolesToAdd)
            {
                var contentRole = new ContentRole
                {
                    ContentId = content.Id,
                    RoleId = role.Id
                };
                dbContext.ContentRoles.Add(contentRole);
            }
        }
    }
    
    private void UpdateContentPropertyValues(IZauberDbContext dbContext, Content content,
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
                mapper.Map(newPropertyValue, existingPropertyValue);
            }
        }
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
}