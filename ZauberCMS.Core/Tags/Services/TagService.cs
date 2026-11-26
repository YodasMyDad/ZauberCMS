using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ZauberCMS.Core.Audit.Interfaces;
using ZauberCMS.Core.Data;
using ZauberCMS.Core.Extensions;
using ZauberCMS.Core.Membership.Interfaces;
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
    ICurrentUserAccessor currentUserAccessor,
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
        var user = await currentUserAccessor.GetUserAsync(cancellationToken);
        var handlerResult = new HandlerResult<Tag>();

        if (!parameters.TagName.IsNullOrWhiteSpace())
        {
            var isUpdate = false;

            var tag = new Tag { TagName = parameters.TagName, SortOrder = parameters.SortOrder, Slug = _slugHelper.GenerateSlug(parameters.TagName)};
            if (parameters.Id != null)
            {
                var dbTag = await dbContext.Tags.FirstOrDefaultAsync(x => x.Id == parameters.Id, cancellationToken);
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
                var dbTag = await dbContext.Tags.FirstOrDefaultAsync(x => x.TagName == parameters.TagName, cancellationToken);
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
        if (parameters.Cached)
        {
            var cacheKey = $"{query.GenerateCacheKey<Tag>()}_Page{parameters.PageIndex}_Amount{parameters.AmountPerPage}";
            return (await cacheService.GetSetCachedItemAsync(cacheKey, async () => await query.ToPaginatedListAsync(parameters.PageIndex, parameters.AmountPerPage, cancellationToken)))!;
        }
        return await query.ToPaginatedListAsync(parameters.PageIndex, parameters.AmountPerPage, cancellationToken);
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
        var user = await currentUserAccessor.GetUserAsync(cancellationToken);
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
        var user = await currentUserAccessor.GetUserAsync(cancellationToken);
        var handlerResult = new HandlerResult<TagItem>();

        if (parameters.ItemId == Guid.Empty)
        {
            handlerResult.AddMessage("ItemId is empty", ResultMessageType.Error);
            return handlerResult;
        }

        // Retrieve existing TagItems for the ItemId
        var existingTagItems = await dbContext.TagItems.AsTracking()
            .Where(x => x.ItemId == parameters.ItemId)
            .ToListAsync(cancellationToken);

        var existingTagIds = existingTagItems.Select(x => x.TagId).ToHashSet();
        var newTagIds = parameters.TagIds.ToHashSet();

        // Determine which TagIds need to be added and removed
        var tagIdsToAdd = newTagIds.Except(existingTagIds).ToList();
        var tagIdsToRemove = existingTagIds.Except(newTagIds).ToList();

        // The links and their audit rows are written by one save, through the save hooks (ZA-018): a veto or a
        // failure leaves neither
        foreach (var tagId in tagIdsToAdd)
        {
            dbContext.TagItems.Add(new TagItem { TagId = tagId, ItemId = parameters.ItemId });
            AddAudit(dbContext, user, $"Created Tag Item (TagId: {tagId}) added");
        }

        foreach (var tagItem in existingTagItems.Where(x => tagIdsToRemove.Contains(x.TagId)))
        {
            dbContext.TagItems.Remove(tagItem);
            AddAudit(dbContext, user, $"Deleted Tag Item (TagId: {tagItem.TagId}) removed");
        }

        var saved = await dbContext.SaveChangesAndLog(Array.Empty<TagItem>(), null, handlerResult, cacheService,
            extensionManager, cancellationToken);
        if (saved.Success)
        {
            cacheService.ClearCachedItemsWithPrefix(nameof(Tag));
        }

        return saved;
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
        var user = await currentUserAccessor.GetUserAsync(cancellationToken);
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
            var tagItems = await dbContext.TagItems.Where(l => l.ItemId == parameters.ItemId).ToListAsync(cancellationToken);
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

    /// <summary>Stages an audit row for the save that follows. Nobody signed in, no row (D27).</summary>
    private static void AddAudit(IZauberDbContext dbContext, Membership.Models.User? user, string action)
    {
        if (user == null) return;
        dbContext.Audits.Add(new Audit.Models.Audit { Description = $"{user.Name} {action}" });
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
