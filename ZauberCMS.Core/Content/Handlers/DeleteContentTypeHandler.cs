using MediatR;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using ZauberCMS.Core.Content.Commands;
using ZauberCMS.Core.Content.Models;
using ZauberCMS.Core.Data;
using ZauberCMS.Core.Extensions;
using ZauberCMS.Core.Membership.Models;
using ZauberCMS.Core.Plugins;
using ZauberCMS.Core.Shared.Models;
using ZauberCMS.Core.Shared.Services;

namespace ZauberCMS.Core.Content.Handlers;

public class DeleteContentTypeHandler(
    IServiceProvider serviceProvider,
    IMediator mediator,
    ICacheService cacheService,
    AuthenticationStateProvider authenticationStateProvider,
    ExtensionManager extensionManager) 
    : IRequestHandler<DeleteContentTypeCommand, HandlerResult<ContentType>>
{
    public async Task<HandlerResult<ContentType>> Handle(DeleteContentTypeCommand request,
        CancellationToken cancellationToken)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ZauberDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var authState = await authenticationStateProvider.GetAuthenticationStateAsync();
        var user = await userManager.GetUserAsync(authState.User);
        var handlerResult = new HandlerResult<ContentType>();

        var contentType = dbContext.ContentTypes.FirstOrDefault(x => x.Id == request.ContentTypeId);
        if (contentType != null)
        {
            await user.AddAudit(contentType, contentType.Name, AuditExtensions.AuditAction.Delete, mediator, cancellationToken);
            dbContext.ContentTypes.Remove(contentType);
            return await dbContext.SaveChangesAndLog(contentType, handlerResult, cacheService, extensionManager, cancellationToken);
        }

        handlerResult.AddMessage("Unable to delete, as no ContentType with that id exists", ResultMessageType.Warning);
        return handlerResult;
    }
}