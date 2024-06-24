using MediatR;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using ZauberCMS.Core.Data;
using ZauberCMS.Core.Extensions;
using ZauberCMS.Core.Media.Commands;
using ZauberCMS.Core.Shared;
using ZauberCMS.Core.Shared.Models;

namespace ZauberCMS.Core.Media.Handlers;

public class DeleteMediaHandler(IServiceProvider serviceProvider, AppState appState, AuthenticationStateProvider authenticationStateProvider) : IRequestHandler<DeleteMediaCommand, HandlerResult<Models.Media>>
{
    public async Task<HandlerResult<Models.Media>> Handle(DeleteMediaCommand request, CancellationToken cancellationToken)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ZauberDbContext>();
        var authState = await authenticationStateProvider.GetAuthenticationStateAsync();
        var handlerResult = new HandlerResult<Models.Media>();
        
        var media = dbContext.Medias.FirstOrDefault(x => x.Id == request.MediaId);
        if (media != null)
        {
            dbContext.Medias.Remove(media);
            await appState.NotifyMediaDeleted(null, authState.User.Identity?.Name!);
            return await dbContext.SaveChangesAndLog(media, handlerResult, cancellationToken);
            
        }

        handlerResult.AddMessage("Unable to delete, as no Media with that id exists", ResultMessageType.Warning);
        return handlerResult;
    }
}