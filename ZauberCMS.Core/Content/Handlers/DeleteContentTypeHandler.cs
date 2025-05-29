using MediatR;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Radzen;
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
        var dbContext = scope.ServiceProvider.GetRequiredService<IZauberDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var authState = await authenticationStateProvider.GetAuthenticationStateAsync();
        var user = await userManager.GetUserAsync(authState.User);
        var handlerResult = new HandlerResult<ContentType>();
        
        // Check if being used by content
        var contentUsingContentType = await mediator.Send(new QueryContentCommand { ContentTypeId = request.ContentTypeId }, cancellationToken);
        if (contentUsingContentType.Items.Any())
        {
            handlerResult.Success = false;
            handlerResult.AddMessage("Unable to delete, because this ContentType is being used", ResultMessageType.Warning);
            return handlerResult;
        }
        
        // Check if it has children
        var children = await mediator.Send(new QueryContentTypesCommand { Query = () => dbContext.ContentTypes.Where(x => x.ParentId == request.ContentTypeId)}, cancellationToken);
        if (children.Items.Any())
        {
            handlerResult.Success = false;
            handlerResult.AddMessage("Unable to delete, because this ContentType has children", ResultMessageType.Warning);
            return handlerResult;
        }
        
        var contentType = dbContext.ContentTypes.FirstOrDefault(x => x.Id == request.ContentTypeId);
        if (contentType != null)
        {
            // Check if this is a composition and if it is, are there any content types using it
            if (contentType.IsComposition)
            {
                var anyUsingThisComposition = await mediator.Send(new QueryContentTypesCommand { Query = () => dbContext.ContentTypes.WhereHasCompositionsUsing(contentType.Id)}, cancellationToken);
                if (anyUsingThisComposition.Items.Any())
                {
                    handlerResult.Success = false;
                    handlerResult.AddMessage("Unable to delete, because there are content types using this composition, remove it first", ResultMessageType.Warning);
                    return handlerResult;
                }
            }
            
            await user.AddAudit(contentType, contentType.Name, AuditExtensions.AuditAction.Delete, mediator, cancellationToken);
            dbContext.ContentTypes.Remove(contentType);
            return await dbContext.SaveChangesAndLog(contentType, handlerResult, cacheService, extensionManager, cancellationToken);
        }

        handlerResult.AddMessage("Unable to delete, as no ContentType with that id exists", ResultMessageType.Warning);
        return handlerResult;
    }
}