using AutoMapper;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ZauberCMS.Core.Data;
using ZauberCMS.Core.Extensions;
using ZauberCMS.Core.Tags.Interfaces;
using ZauberCMS.Core.Tags.Models;
using ZauberCMS.Core.Tags.Parameters;
using ZauberCMS.Core.Membership.Models;
using ZauberCMS.Core.Plugins;
using ZauberCMS.Core.Shared.Models;
using ZauberCMS.Core.Shared.Services;

namespace ZauberCMS.Core.Tags.Services;

public class TagService(
    IServiceProvider serviceProvider,
    ICacheService cacheService,
    IMapper mapper,
    AuthenticationStateProvider authenticationStateProvider,
    ExtensionManager extensionManager) : ITagService
{
    public async Task<HandlerResult<Tag>> SaveTagAsync(SaveTagParameters parameters, CancellationToken cancellationToken = default)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IZauberDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var authState = await authenticationStateProvider.GetAuthenticationStateAsync();
        var user = await userManager.GetUserAsync(authState.User);
        var handlerResult = new HandlerResult<Tag>();
        var isUpdate = false;

        if (parameters.Tag != null)
        {
            var tag = dbContext.Tags.FirstOrDefault(x => x.Id == parameters.Tag.Id);

            if (tag == null)
            {
                tag = parameters.Tag;
                dbContext.Tags.Add(tag);
            }
            else
            {
                isUpdate = true;
                mapper.Map(parameters.Tag, tag);
                tag.DateUpdated = DateTime.UtcNow;
            }

            await user.AddAudit(tag, tag.Name, isUpdate ? AuditExtensions.AuditAction.Update : AuditExtensions.AuditAction.Create, null, cancellationToken);
            return await dbContext.SaveChangesAndLog(tag, handlerResult, cacheService, extensionManager, cancellationToken);
        }

        handlerResult.AddMessage("Tag is null", ResultMessageType.Error);
        return handlerResult;
    }

    public async Task<HandlerResult<Tag>> QueryTagAsync(QueryTagParameters parameters, CancellationToken cancellationToken = default)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IZauberDbContext>();
        var query = dbContext.Tags.AsQueryable();

        if (parameters.AsNoTracking)
        {
            query = query.AsNoTracking();
        }

        if (parameters.IncludeTagItems)
        {
            query = query.Include(x => x.TagItems);
        }

        if (!parameters.SearchTerm.IsNullOrWhiteSpace())
        {
            query = query.Where(x => x.Name.Contains(parameters.SearchTerm));
        }

        if (parameters.ContentId.HasValue)
        {
            query = query.Where(x => x.TagItems.Any(ti => ti.ContentId == parameters.ContentId));
        }

        if (parameters.WhereClause != null)
        {
            query = query.Where(parameters.WhereClause);
        }

        if (!parameters.OrderBy.IsNullOrWhiteSpace())
        {
            query = query.OrderBy(parameters.OrderBy);
        }
        else
        {
            query = query.OrderBy(x => x.Name);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        if (parameters.AmountPerPage > 0)
        {
            query = query.Skip(parameters.PageIndex * parameters.AmountPerPage).Take(parameters.AmountPerPage);
        }

        var items = await query.ToListAsync(cancellationToken);

        return new HandlerResult<Tag>
        {
            Success = true,
            Items = items,
            TotalCount = totalCount
        };
    }

    public async Task<HandlerResult<Tag>> DeleteTagAsync(DeleteTagParameters parameters, CancellationToken cancellationToken = default)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IZauberDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var authState = await authenticationStateProvider.GetAuthenticationStateAsync();
        var user = await userManager.GetUserAsync(authState.User);
        var handlerResult = new HandlerResult<Tag>();

        var tag = await dbContext.Tags
            .Include(t => t.TagItems)
            .FirstOrDefaultAsync(x => x.Id == parameters.Id, cancellationToken);

        if (tag != null)
        {
            // Remove all tag items first
            dbContext.TagItems.RemoveRange(tag.TagItems);
            
            await user.AddAudit(tag, tag.Name, AuditExtensions.AuditAction.Delete, null, cancellationToken);
            dbContext.Tags.Remove(tag);
            await dbContext.SaveChangesAsync(cancellationToken);
            handlerResult.Messages.Add(new ResultMessage("Tag deleted successfully", ResultMessageType.Success));
            handlerResult.Success = true;
        }
        else
        {
            handlerResult.AddMessage("Tag not found", ResultMessageType.Error);
        }

        return handlerResult;
    }

    public async Task<HandlerResult<TagItem>> SaveTagItemAsync(SaveTagItemParameters parameters, CancellationToken cancellationToken = default)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IZauberDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var authState = await authenticationStateProvider.GetAuthenticationStateAsync();
        var user = await userManager.GetUserAsync(authState.User);
        var handlerResult = new HandlerResult<TagItem>();
        var isUpdate = false;

        if (parameters.TagItem != null)
        {
            var tagItem = dbContext.TagItems.FirstOrDefault(x => x.Id == parameters.TagItem.Id);

            if (tagItem == null)
            {
                tagItem = parameters.TagItem;
                dbContext.TagItems.Add(tagItem);
            }
            else
            {
                isUpdate = true;
                mapper.Map(parameters.TagItem, tagItem);
                tagItem.DateUpdated = DateTime.UtcNow;
            }

            await user.AddAudit(tagItem, $"TagItem ({tagItem.TagId})", isUpdate ? AuditExtensions.AuditAction.Update : AuditExtensions.AuditAction.Create, null, cancellationToken);
            return await dbContext.SaveChangesAndLog(tagItem, handlerResult, cacheService, extensionManager, cancellationToken);
        }

        handlerResult.AddMessage("TagItem is null", ResultMessageType.Error);
        return handlerResult;
    }

    public async Task<HandlerResult<TagItem>> DeleteTagItemAsync(DeleteTagItemParameters parameters, CancellationToken cancellationToken = default)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IZauberDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var authState = await authenticationStateProvider.GetAuthenticationStateAsync();
        var user = await userManager.GetUserAsync(authState.User);
        var handlerResult = new HandlerResult<TagItem>();

        var tagItem = await dbContext.TagItems
            .FirstOrDefaultAsync(x => x.Id == parameters.Id, cancellationToken);

        if (tagItem != null)
        {
            await user.AddAudit(tagItem, $"TagItem ({tagItem.TagId})", AuditExtensions.AuditAction.Delete, null, cancellationToken);
            dbContext.TagItems.Remove(tagItem);
            await dbContext.SaveChangesAsync(cancellationToken);
            handlerResult.Messages.Add(new ResultMessage("Tag item deleted successfully", ResultMessageType.Success));
            handlerResult.Success = true;
        }
        else
        {
            handlerResult.AddMessage("Tag item not found", ResultMessageType.Error);
        }

        return handlerResult;
    }
}