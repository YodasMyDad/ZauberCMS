using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ZauberCMS.Core.Audit.Interfaces;
using ZauberCMS.Core.Data;
using ZauberCMS.Core.Extensions;
using ZauberCMS.Core.Membership.Models;
using ZauberCMS.Core.Plugins;
using ZauberCMS.Core.Shared.Models;
using ZauberCMS.Core.Shared.Services;
using ZauberCMS.Core.Tags.Interfaces;
using ZauberCMS.Core.Tags.Models;
using ZauberCMS.Core.Tags.Parameters;

namespace ZauberCMS.Core.Tags.Services;

public class TagService(
    IServiceScopeFactory serviceScopeFactory,
    ICacheService cacheService,
    AuthenticationStateProvider authenticationStateProvider,
    ExtensionManager extensionManager)
    : ITagService
{
    private readonly SlugHelper _slugHelper = new();

    /// <summary>
    /// Creates or updates a tag, generating a slug. Logs audit.
    /// </summary>
    /// <param name="parameters">Tag name, sort order and optional id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result including success and messages.</returns>
    public async Task<HandlerResult<Tag>> SaveTagAsync(SaveTagParameters parameters, CancellationToken cancellationToken = default)
    {
        using var scope = serviceScopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IZauberDbContext>();
        var authState = await authenticationStateProvider.GetAuthenticationStateAsync();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var user = await userManager.GetUserAsync(authState.User);
        var handlerResult = new HandlerResult<Tag>();

        if (!parameters.TagName.IsNullOrWhiteSpace())
        {
            var isUpdate = false;

            var tag = new Tag { TagName = parameters.TagName, SortOrder = parameters.SortOrder, Slug = _slugHelper.GenerateSlug(parameters.TagName)};
            if (parameters.Id != null)
            {
                var dbTag = dbContext.Tags.FirstOrDefault(x => x.Id == parameters.Id);
                if (dbTag != null)
                {
                    isUpdate = true;
                    tag = dbTag;
                    tag.TagName = parameters.TagName;
                    tag.SortOrder = parameters.SortOrder;
                    tag.Slug = _slugHelper.GenerateSlug(parameters.TagName);
                }
            }
            else
            {
                var dbTag = dbContext.Tags.FirstOrDefault(x => x.TagName == parameters.TagName);
                if (dbTag != null)
                {
                    if (parameters.TagName == dbTag.TagName)
                    {
                        // Just return if they are trying to save the same tag
                        handlerResult.Success = true;
                        return handlerResult;
                    }
                }
            }

            if (!isUpdate)
            {
                dbContext.Tags.Add(tag);
            }
            else
            {
                tag.DateUpdated = DateTime.UtcNow;
            }

            var auditService = scope.ServiceProvider.GetRequiredService<IAuditService>();
            await user.AddAudit(tag, $"Tag ({tag.TagName})",
                isUpdate ? AuditExtensions.AuditAction.Update : AuditExtensions.AuditAction.Create, auditService,
                cancellationToken);
            return await dbContext.SaveChangesAndLog(tag, handlerResult, cacheService, extensionManager, cancellationToken);
        }

        handlerResult.AddMessage("Tag Name is null", ResultMessageType.Error);
        return handlerResult;
    }

    /// <summary>
    /// Queries tags with filtering, ordering and paging. Can use cache.
    /// </summary>
    /// <param name="parameters">Query options including names, slugs and item ids.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Paged list of tags.</returns>
    #pragma warning disable CS1998
    public async Task<PaginatedList<Tag>> QueryTagAsync(QueryTagParameters parameters, CancellationToken cancellationToken = default)
    {
        using var scope = serviceScopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IZauberDbContext>();
        var query = BuildQuery(parameters, dbContext);
        var cacheKey = $"{query.GenerateCacheKey<Tag>()}_Page{parameters.PageIndex}_Amount{parameters.AmountPerPage}";
        if (parameters.Cached)
        {
            return (await cacheService.GetSetCachedItemAsync(cacheKey, async () => query.ToPaginatedList(parameters.PageIndex, parameters.AmountPerPage)))!;
        }
        return query.ToPaginatedList(parameters.PageIndex, parameters.AmountPerPage);
    }
    #pragma warning restore CS1998

    /// <summary>
    /// Deletes a tag by id or name. Logs audit.
    /// </summary>
    /// <param name="parameters">Tag id or name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result including success and messages.</returns>
    public async Task<HandlerResult<Tag?>> DeleteTagAsync(DeleteTagParameters parameters, CancellationToken cancellationToken = default)
    {
        using var scope = serviceScopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IZauberDbContext>();
        var authState = await authenticationStateProvider.GetAuthenticationStateAsync();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var user = await userManager.GetUserAsync(authState.User);
        var handlerResult = new HandlerResult<Tag>();

        if (parameters.Id != null)
        {
            var tag =
                await dbContext.Tags.FirstOrDefaultAsync(l => l.Id == parameters.Id,
                    cancellationToken: cancellationToken);
            if (tag != null)
            {
                var auditService = scope.ServiceProvider.GetRequiredService<IAuditService>();
                await user.AddAudit(tag, $"Tag ({tag.TagName})",
                    AuditExtensions.AuditAction.Delete, auditService,
                    cancellationToken);
                dbContext.Tags.Remove(tag);
            }
        }
        else
        {
            var tag =
                await dbContext.Tags.FirstOrDefaultAsync(l => l.TagName == parameters.TagName,
                    cancellationToken: cancellationToken);
            if (tag != null)
            {
                var auditService = scope.ServiceProvider.GetRequiredService<IAuditService>();
                await user.AddAudit(tag, $"Tag ({tag.TagName})",
                    AuditExtensions.AuditAction.Delete, auditService,
                    cancellationToken);
                dbContext.Tags.Remove(tag);
            }
        }

        return (await dbContext.SaveChangesAndLog(null, handlerResult, cacheService, extensionManager, cancellationToken))!;
    }

    /// <summary>
    /// Synchronizes tag assignments for a given item id by adding/removing TagItems. Logs audit entries.
    /// </summary>
    /// <param name="parameters">Item id and desired tag id set.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result including success and messages.</returns>
    public async Task<HandlerResult<TagItem>> SaveTagItemAsync(SaveTagItemParameters parameters, CancellationToken cancellationToken = default)
    {
        using var scope = serviceScopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IZauberDbContext>();
        var authState = await authenticationStateProvider.GetAuthenticationStateAsync();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var user = await userManager.GetUserAsync(authState.User);
        var handlerResult = new HandlerResult<TagItem>();

        if (parameters.ItemId == Guid.Empty)
        {
            handlerResult.AddMessage("ItemId is empty", ResultMessageType.Error);
            return handlerResult;
        }

        // Retrieve existing TagItems for the ItemId
        var existingTagItems = dbContext.TagItems
            .Where(x => x.ItemId == parameters.ItemId)
            .ToList();

        var existingTagIds = existingTagItems.Select(x => x.TagId).ToHashSet();
        var newTagIds = parameters.TagIds.ToHashSet();

        // Determine which TagIds need to be added and removed
        var tagIdsToAdd = newTagIds.Except(existingTagIds).ToList();
        var tagIdsToRemove = existingTagIds.Except(newTagIds).ToList();

        // Add new TagItems
        foreach (var tagId in tagIdsToAdd)
        {
            var tagItem = new TagItem { TagId = tagId, ItemId = parameters.ItemId };
            dbContext.TagItems.Add(tagItem);

            var auditService = scope.ServiceProvider.GetRequiredService<IAuditService>();
            await user.AddAudit(tagItem, $"Tag Item (TagId: {tagId}) added",
                AuditExtensions.AuditAction.Create, auditService,
                cancellationToken);
        }

        // Remove TagItems that are no longer associated
        foreach (var tagId in tagIdsToRemove)
        {
            var tagItem = existingTagItems.FirstOrDefault(x => x.TagId == tagId);
            if (tagItem != null)
            {
                dbContext.TagItems.Remove(tagItem);

                var auditService = scope.ServiceProvider.GetRequiredService<IAuditService>();
                await user.AddAudit(tagItem, $"Tag Item (TagId: {tagId}) removed",
                    AuditExtensions.AuditAction.Delete, auditService,
                    cancellationToken);
            }
        }

        // Save changes and update the handler result
        await dbContext.SaveChangesAsync(cancellationToken);
        handlerResult.Success = true;

        return handlerResult;
    }

    /// <summary>
    /// Deletes a tag item by id or all tag items for a given item id. Logs audit.
    /// </summary>
    /// <param name="parameters">TagItem id or item id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result including success and messages.</returns>
    public async Task<HandlerResult<TagItem?>> DeleteTagItemAsync(DeleteTagItemParameters parameters, CancellationToken cancellationToken = default)
    {
        using var scope = serviceScopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IZauberDbContext>();
        var auditService = scope.ServiceProvider.GetRequiredService<IAuditService>();
        var authState = await authenticationStateProvider.GetAuthenticationStateAsync();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var user = await userManager.GetUserAsync(authState.User);
        var handlerResult = new HandlerResult<TagItem>();

        TagItem? tagItem = null;
        if (parameters.TagId != null)
        {
            tagItem =
                await dbContext.TagItems.FirstOrDefaultAsync(l => l.Id == parameters.TagId,
                    cancellationToken: cancellationToken);
            if (tagItem != null)
            {
                await user.AddAudit(tagItem, $"TagItem ({tagItem.TagId})",
                    AuditExtensions.AuditAction.Delete, auditService,
                    cancellationToken);
                dbContext.TagItems.Remove(tagItem);
            }
        }
        
        if (parameters.ItemId != null)
        {
            var tagItems = dbContext.TagItems.Where(l => l.ItemId == parameters.ItemId).ToList();
            if (tagItems.Any())
            {
                foreach (var ti in tagItems)
                {
                    await user.AddAudit(ti, $"TagItem ({ti.TagId})",
                        AuditExtensions.AuditAction.Delete, auditService,
                        cancellationToken);
                    dbContext.TagItems.Remove(ti);
                }
            }
        }

        return (await dbContext.SaveChangesAndLog(tagItem, handlerResult, cacheService, extensionManager, cancellationToken))!;
    }

    private static IQueryable<Tag> BuildQuery(QueryTagParameters parameters, IZauberDbContext dbContext)
    {
        var query = dbContext.Tags.AsQueryable();

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

            if (parameters.Ids.Count != 0)
            {
                query = query.Where(x => parameters.Ids.Contains(x.Id));
            }

            if (parameters.TagNames.Count != 0)
            {
                query = query.Where(x => parameters.TagNames.Contains(x.TagName));
            }

            if (parameters.TagSlugs.Count != 0)
            {
                query = query.Where(x => parameters.TagSlugs.Contains(x.Slug));
            }

            if (parameters.ItemIds.Count != 0)
            {
                query = query.Include(x => x.TagItems)
                             .Where(x => x.TagItems.Any(ti => parameters.ItemIds.Contains(ti.ItemId)))
                             .AsSplitQuery();
            }
        }

        if (parameters.WhereClause != null)
        {
            query = query.Where(parameters.WhereClause);
        }

        query = parameters.OrderBy switch
        {
            GetTagOrderBy.DateCreated => query.OrderBy(p => p.DateCreated),
            GetTagOrderBy.DateCreatedDescending => query.OrderByDescending(p => p.DateCreated),
            GetTagOrderBy.TagName => query.OrderBy(p => p.TagName),
            GetTagOrderBy.TagNameDescending => query.OrderByDescending(p => p.TagName),
            _ => query.OrderBy(p => p.SortOrder)
        };

        return query;
    }
}
