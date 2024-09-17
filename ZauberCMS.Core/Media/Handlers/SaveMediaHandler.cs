using AutoMapper;
using MediatR;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using ZauberCMS.Core.Data;
using ZauberCMS.Core.Extensions;
using ZauberCMS.Core.Media.Commands;
using ZauberCMS.Core.Media.Models;
using ZauberCMS.Core.Membership.Models;
using ZauberCMS.Core.Providers;
using ZauberCMS.Core.Shared;
using ZauberCMS.Core.Shared.Models;
using ZauberCMS.Core.Shared.Services;

namespace ZauberCMS.Core.Media.Handlers;

public class SaveMediaHandler(ProviderService providerService, 
    IServiceProvider serviceProvider, 
    IMapper mapper, 
    AppState appState, 
    IMediator mediator,
    ICacheService cacheService,
    AuthenticationStateProvider authenticationStateProvider)
    : IRequestHandler<SaveMediaCommand, HandlerResult<Models.Media>>
{
    public async Task<HandlerResult<Models.Media>> Handle(SaveMediaCommand request, CancellationToken cancellationToken)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ZauberDbContext>();
        
        var result = new HandlerResult<Models.Media>();
        var authState = await authenticationStateProvider.GetAuthenticationStateAsync();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var user = await userManager.GetUserAsync(authState.User);
        
        // If we are either creating a new file or over writing the current one
        if (request.FileToSave != null)
        {
            result = await providerService.StorageProvider!.SaveFile(request.FileToSave, request.MediaToSave);
            if (!result.Success)
            {
                return result;
            }
        }
        else if (request.MediaToSave != null)
        {
            result.Entity = request.MediaToSave;
        }

        if (result.Entity != null)
        {
            if (request.ParentFolderId != null)
            {
                result.Entity.ParentId = request.ParentFolderId;
            }
            
            result.Entity.LastUpdatedById = user!.Id;
            
            // Now update or add the media item
            if (request.IsUpdate)
            {
                // Get the DB version
                var dbMedia = dbContext.Medias
                    .FirstOrDefault(x => x.Id ==  result.Entity.Id);
                // Map the updated properties
                mapper.Map(result.Entity, dbMedia);
                if (dbMedia != null) dbMedia.DateUpdated = DateTime.UtcNow;

                if (result.Entity.Url.IsNullOrWhiteSpace() && result.Entity.MediaType != MediaType.Folder)
                {
                    result.AddMessage("Url cannot be empty", ResultMessageType.Error);
                    return result;
                }
                await user.AddAudit(result.Entity, result.Entity.Name, AuditExtensions.AuditAction.Update, mediator, cancellationToken);
                result = await dbContext.SaveChangesAndLog(result.Entity, result, cacheService, cancellationToken);
                if (dbMedia != null) await appState.NotifyMediaSaved(dbMedia, authState.User.Identity?.Name!);
            }
            else
            {
                dbContext.Medias.Add(result.Entity);
                await user.AddAudit(result.Entity, result.Entity.Name, AuditExtensions.AuditAction.Create, mediator, cancellationToken);
                result = await dbContext.SaveChangesAndLog(result.Entity, result, cacheService, cancellationToken);
                await appState.NotifyMediaSaved(result.Entity, authState.User.Identity?.Name!);
            }   
        }
        else
        {
            result.AddMessage("There is no media to save?", ResultMessageType.Error);
            result.Success = false;
        }

        return result;
    }
}