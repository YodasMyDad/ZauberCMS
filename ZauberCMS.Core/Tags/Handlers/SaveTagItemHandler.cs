using MediatR;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using ZauberCMS.Core.Data;
using ZauberCMS.Core.Extensions;
using ZauberCMS.Core.Membership.Models;
using ZauberCMS.Core.Shared.Models;
using ZauberCMS.Core.Tags.Commands;
using ZauberCMS.Core.Tags.Models;

namespace ZauberCMS.Core.Tags.Handlers;

public class SaveTagItemHandler(
    IServiceProvider serviceProvider,
    IMediator mediator,
    AuthenticationStateProvider authenticationStateProvider)
    : IRequestHandler<SaveTagItemCommand, HandlerResult<TagItem>>
{
    public async Task<HandlerResult<TagItem>> Handle(SaveTagItemCommand request, CancellationToken cancellationToken)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ZauberDbContext>();
        var authState = await authenticationStateProvider.GetAuthenticationStateAsync();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var user = await userManager.GetUserAsync(authState.User);
        var handlerResult = new HandlerResult<TagItem>();

        if (request.ItemId == Guid.Empty)
        {
            handlerResult.AddMessage("ItemId is empty", ResultMessageType.Error);
            return handlerResult;
        }

        // Retrieve existing TagItems for the ItemId
        var existingTagItems = dbContext.TagItems
            .Where(x => x.ItemId == request.ItemId)
            .ToList();

        var existingTagIds = existingTagItems.Select(x => x.TagId).ToHashSet();
        var newTagIds = request.TagIds.ToHashSet();

        // Determine which TagIds need to be added and removed
        var tagIdsToAdd = newTagIds.Except(existingTagIds).ToList();
        var tagIdsToRemove = existingTagIds.Except(newTagIds).ToList();

        // Add new TagItems
        foreach (var tagId in tagIdsToAdd)
        {
            var tagItem = new TagItem { TagId = tagId, ItemId = request.ItemId };
            dbContext.TagItems.Add(tagItem);

            await user.AddAudit(tagItem, $"Tag Item (TagId: {tagId}) added",
                AuditExtensions.AuditAction.Create, mediator,
                cancellationToken);
        }

        // Remove TagItems that are no longer associated
        foreach (var tagId in tagIdsToRemove)
        {
            var tagItem = existingTagItems.FirstOrDefault(x => x.TagId == tagId);
            if (tagItem != null)
            {
                dbContext.TagItems.Remove(tagItem);

                await user.AddAudit(tagItem, $"Tag Item (TagId: {tagId}) removed",
                    AuditExtensions.AuditAction.Delete, mediator,
                    cancellationToken);
            }
        }

        // Save changes and update the handler result
        await dbContext.SaveChangesAsync(cancellationToken);
        handlerResult.Success = true;

        return handlerResult;
    }
}