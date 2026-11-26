using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ZauberCMS.Core.Content.Parameters;
using ZauberCMS.Core.Data;
using ZauberCMS.Core.Extensions;
using ZauberCMS.Core.Membership.Models;
using ZauberCMS.Core.Shared.Models;

namespace ZauberCMS.Core.Content.Services;

// Public access: which roles may see a page and the pages below it.
public partial class ContentService
{
    /// <inheritdoc />
    public async Task<HandlerResult<Models.Content>> SetContentRolesAsync(SetContentRolesParameters parameters,
        CancellationToken cancellationToken = default)
    {
        using var scope = serviceScopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IZauberDbContext>();
        var authState = await authenticationStateProvider.GetAuthenticationStateAsync();
        var user = await GetCurrentUserAsync(authState);
        var handlerResult = new HandlerResult<Models.Content>();

        var content = await dbContext.Contents.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == parameters.ContentId && !x.Deleted, cancellationToken);
        if (content == null)
        {
            handlerResult.AddMessage("Content not found", ResultMessageType.Error);
            return handlerResult;
        }

        // Access set on an ancestor covers this page; it is changed there
        var ancestorIds = content.Path.Where(id => id != content.Id).ToList();
        if (ancestorIds.Count != 0 &&
            await dbContext.ContentRoles.AnyAsync(r => ancestorIds.Contains(r.ContentId), cancellationToken))
        {
            handlerResult.AddMessage(
                "Cannot change access to this content as it has roles already defined by an ancestor.",
                ResultMessageType.Error);
            return handlerResult;
        }

        // The page and every page below it (Path holds the item itself), in one change: this was a full save,
        // a version and a cache flush per page, with no transaction (ZA-073)
        var ids = await dbContext.Contents.WherePathLike(content.Id).Select(x => x.Id).ToListAsync(cancellationToken);
        if (!ids.Contains(content.Id))
        {
            ids.Add(content.Id);
        }

        var roleIds = parameters.RoleIds.Distinct().ToList();
        var existing = await dbContext.ContentRoles.Where(r => ids.Contains(r.ContentId))
            .ToListAsync(cancellationToken);
        var removed = existing.Where(r => !roleIds.Contains(r.RoleId)).ToList();
        var present = existing.Select(r => (r.ContentId, r.RoleId)).ToHashSet();
        var added = ids
            .SelectMany(id => roleIds
                .Where(roleId => !present.Contains((id, roleId)))
                .Select(roleId => new ContentRole { ContentId = id, RoleId = roleId }))
            .ToList();
        dbContext.ContentRoles.RemoveRange(removed);
        dbContext.ContentRoles.AddRange(added);

        if (user != null)
        {
            // Saved by the SaveChanges below, with the roles
            dbContext.Audits.Add(new Audit.Models.Audit
            {
                Description = roleIds.Count == 0
                    ? $"{user.Name} removed public access from {content.Name} and the pages below it"
                    : $"{user.Name} changed public access of {content.Name} and the pages below it"
            });
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var saved = await dbContext.SaveChangesAndLog([..removed, ..added], null, new HandlerResult<ContentRole>(),
            cacheService, extensionManager, cancellationToken);
        if (!saved.Success)
        {
            await transaction.RollbackAsync(cancellationToken);
            handlerResult.Messages.AddRange(saved.Messages);
            return handlerResult;
        }

        await transaction.CommitAsync(cancellationToken);
        // The front end caches pages with their roles
        cacheService.ClearCachedItemsWithPrefix(nameof(Models.Content));
        handlerResult.Success = true;
        handlerResult.Entity = content;
        return handlerResult;
    }
}
