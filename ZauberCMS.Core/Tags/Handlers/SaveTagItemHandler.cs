using MediatR;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using ZauberCMS.Core.Data;
using ZauberCMS.Core.Extensions;
using ZauberCMS.Core.Membership.Models;
using ZauberCMS.Core.Shared.Models;
using ZauberCMS.Core.Shared.Services;
using ZauberCMS.Core.Tags.Commands;
using ZauberCMS.Core.Tags.Models;

namespace ZauberCMS.Core.Tags.Handlers;

public class SaveTagItemHandler(
    IServiceProvider serviceProvider,
    IMediator mediator,
    ICacheService cacheService,
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

        if (request.TagId != Guid.Empty && request.ItemId != Guid.Empty)
        {
            var tagItem = new TagItem { TagId = request.TagId, ItemId = request.ItemId };
            var dbTagItem = dbContext.TagItems.FirstOrDefault(x => x.TagId == tagItem.TagId && x.ItemId == tagItem.ItemId);
            if (dbTagItem != null)
            {
                // Just return if they are trying to save the same tag
                handlerResult.Success = true;
                return handlerResult;
            }

            dbContext.TagItems.Add(tagItem);


            await user.AddAudit(tagItem, $"Tag Item (tagid: {tagItem.TagId})",
                AuditExtensions.AuditAction.Create, mediator,
                cancellationToken);
            return await dbContext.SaveChangesAndLog(tagItem, handlerResult, cacheService, cancellationToken);
        }

        handlerResult.AddMessage("Both Id's are empty", ResultMessageType.Error);
        return handlerResult;
    }
}