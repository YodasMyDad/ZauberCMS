using MediatR;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ZauberCMS.Core.Data;
using ZauberCMS.Core.Extensions;
using ZauberCMS.Core.Membership.Models;
using ZauberCMS.Core.Shared.Models;
using ZauberCMS.Core.Shared.Services;
using ZauberCMS.Core.Tags.Commands;
using ZauberCMS.Core.Tags.Models;

namespace ZauberCMS.Core.Tags.Handlers;

public class DeleteTagItemHandler(
    IServiceProvider serviceProvider,
    IMediator mediator,
    ICacheService cacheService,
    AuthenticationStateProvider authenticationStateProvider) : IRequestHandler<DeleteTagItemCommand, HandlerResult<TagItem?>>
{
    public async Task<HandlerResult<TagItem?>> Handle(DeleteTagItemCommand request,
        CancellationToken cancellationToken)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ZauberDbContext>();
        var authState = await authenticationStateProvider.GetAuthenticationStateAsync();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var user = await userManager.GetUserAsync(authState.User);
        var handlerResult = new HandlerResult<TagItem>();

        if (request.TagId != null)
        {
            var tagItem =
                await dbContext.TagItems.FirstOrDefaultAsync(l => l.Id == request.TagId,
                    cancellationToken: cancellationToken);
            if (tagItem != null)
            {
                await user.AddAudit(tagItem, $"TagItem ({tagItem.TagId})",
                    AuditExtensions.AuditAction.Delete, mediator,
                    cancellationToken);
                dbContext.TagItems.Remove(tagItem);
            }
        }
        
        if (request.ItemId != null)
        {
            var tagItems = dbContext.TagItems.Where(l => l.ItemId == request.ItemId).ToList();
            if (tagItems.Any())
            {
                foreach (var tagItem in tagItems)
                {
                    await user.AddAudit(tagItem, $"TagItem ({tagItem.TagId})",
                        AuditExtensions.AuditAction.Delete, mediator,
                        cancellationToken);
                    dbContext.TagItems.Remove(tagItem);
                }
            }
        }

        return (await dbContext.SaveChangesAndLog(null, handlerResult, cacheService, cancellationToken))!;
    }
}