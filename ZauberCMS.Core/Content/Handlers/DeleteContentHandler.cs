using MediatR;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ZauberCMS.Core.Content.Commands;
using ZauberCMS.Core.Data;
using ZauberCMS.Core.Extensions;
using ZauberCMS.Core.Shared;
using ZauberCMS.Core.Shared.Models;

namespace ZauberCMS.Core.Content.Handlers;

public class DeleteContentHandler(IServiceProvider serviceProvider, AppState appState, AuthenticationStateProvider authenticationStateProvider) : IRequestHandler<DeleteContentCommand, HandlerResult<Models.Content>>
{
    public async Task<HandlerResult<Models.Content>> Handle(DeleteContentCommand request, CancellationToken cancellationToken)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ZauberDbContext>();
        var authState = await authenticationStateProvider.GetAuthenticationStateAsync();
        var handlerResult = new HandlerResult<Models.Content>();
        
        var content = dbContext.Contents.FirstOrDefault(x => x.Id == request.ContentId);
        if (content != null)
        {
            //Check if it has children
            var children = dbContext.Contents.AsNoTracking().Where(x => x.ParentId == content.Id);
            if (children.Any())
            {
                handlerResult.AddMessage("Unable to delete content with child content, delete or move those items first", ResultMessageType.Error);
                return handlerResult;
            }

            // Now delete the PropertyData
            var propertyDataToDelete = dbContext.ContentPropertyValues.Where(x => x.ContentId == content.Id);
            foreach (var contentPropertyValue in propertyDataToDelete)
            {
                dbContext.ContentPropertyValues.Remove(contentPropertyValue);
            }

            content.PropertyData.Clear();
            dbContext.Contents.Remove(content);
            await appState.NotifyContentDeleted(null, authState.User.Identity?.Name!);
            return await dbContext.SaveChangesAndLog(content, handlerResult, cancellationToken);
        }

        handlerResult.AddMessage("Unable to delete, as no Content with that id exists", ResultMessageType.Warning);
        return handlerResult;
    }
}