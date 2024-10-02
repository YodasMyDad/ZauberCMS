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

public class SaveTagHandler(
    IServiceProvider serviceProvider,
    IMediator mediator,
    ICacheService cacheService,
    AuthenticationStateProvider authenticationStateProvider)
    : IRequestHandler<SaveTagCommand, HandlerResult<Tag>>
{
    public async Task<HandlerResult<Tag>> Handle(SaveTagCommand request, CancellationToken cancellationToken)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ZauberDbContext>();
        var authState = await authenticationStateProvider.GetAuthenticationStateAsync();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var user = await userManager.GetUserAsync(authState.User);
        var handlerResult = new HandlerResult<Tag>();

        if (!request.TagName.IsNullOrWhiteSpace())
        {
            var isUpdate = false;

            var tag = new Tag { TagName = request.TagName };
            if (request.Id != null)
            {
                var dbTag = dbContext.Tags.FirstOrDefault(x => x.Id == request.Id);
                if (dbTag != null)
                {
                    if (request.TagName == dbTag.TagName)
                    {
                        // Just return if they are trying to save the same tag
                        handlerResult.Success = true;
                        return handlerResult;
                    }

                    isUpdate = true;
                    tag = dbTag;
                    tag.TagName = request.TagName;
                }
            }
            else
            {
                var dbTag = dbContext.Tags.FirstOrDefault(x => x.TagName == request.TagName);
                if (dbTag != null)
                {
                    if (request.TagName == dbTag.TagName)
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

            await user.AddAudit(tag, $"Tag ({tag.TagName})",
                isUpdate ? AuditExtensions.AuditAction.Update : AuditExtensions.AuditAction.Create, mediator,
                cancellationToken);
            return await dbContext.SaveChangesAndLog(tag, handlerResult, cacheService, cancellationToken);
        }

        handlerResult.AddMessage("Tag Name is null", ResultMessageType.Error);
        return handlerResult;
    }
}