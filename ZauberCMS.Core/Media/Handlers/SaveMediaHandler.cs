using AutoMapper;
using MediatR;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using ZauberCMS.Core.Data;
using ZauberCMS.Core.Extensions;
using ZauberCMS.Core.Media.Commands;
using ZauberCMS.Core.Providers;
using ZauberCMS.Core.Shared;
using ZauberCMS.Core.Shared.Models;

namespace ZauberCMS.Core.Media.Handlers;

public class SaveMediaHandler(ProviderService providerService, IServiceProvider serviceProvider, IMapper mapper, AppState appState, AuthenticationStateProvider authenticationStateProvider)
    : IRequestHandler<SaveMediaCommand, HandlerResult<Models.Media>>
{

    public async Task<HandlerResult<Models.Media>> Handle(SaveMediaCommand request, CancellationToken cancellationToken)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ZauberDbContext>();
        
        var result = new HandlerResult<Models.Media>();
        var authState = await authenticationStateProvider.GetAuthenticationStateAsync();
        var isUpdate = request.MediaToSave != null;
        
        // If we are either creating a new file or over writing the current one
        if (request.FileToSave != null)
        {
            result = await providerService.StorageProvider!.SaveFile(request.FileToSave, request.MediaToSave);
        }
        
        // Now update or add the media item
        if (isUpdate)
        {
            // Get the DB version
            var dbMedia = dbContext.Medias
                .FirstOrDefault(x => x.Id ==  result.Entity.Id);
            // Map the updated properties
            mapper.Map(result.Entity, dbMedia);
            if (dbMedia != null) dbMedia.DateUpdated = DateTime.UtcNow;
            result = await dbContext.SaveChangesAndLog(result.Entity, result, cancellationToken);
            if (dbMedia != null) await appState.NotifyMediaSaved(dbMedia, authState.User.Identity?.Name!);
        }
        else
        {
            dbContext.Medias.Add(result.Entity);
            result = await dbContext.SaveChangesAndLog(result.Entity, result, cancellationToken);
            await appState.NotifyMediaSaved(result.Entity, authState.User.Identity?.Name!);
        }

        return result;
    }
}