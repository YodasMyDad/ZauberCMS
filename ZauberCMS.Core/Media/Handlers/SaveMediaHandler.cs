using MediatR;
using Microsoft.Extensions.DependencyInjection;
using ZauberCMS.Core.Data;
using ZauberCMS.Core.Extensions;
using ZauberCMS.Core.Media.Commands;
using ZauberCMS.Core.Providers;
using ZauberCMS.Core.Shared.Models;

namespace ZauberCMS.Core.Media.Handlers;

public class SaveMediaHandler(ProviderService providerService, IServiceProvider serviceProvider) 
    : IRequestHandler<SaveMediaCommand, List<FileSaveResult>>
{
    public async Task<List<FileSaveResult>> Handle(SaveMediaCommand request, CancellationToken cancellationToken)
    {
        return request.FilesToSave.Count != 0
            ? await SaveFiles(request.FilesToSave)
            : await SaveMedia(request.MediaToSave);
    }

    private async Task<List<FileSaveResult>> SaveFiles(List<FileSaveResult> files)
    {
        var results = new List<FileSaveResult>();
        
        if (files.Count > 0)
        {
            using var scope = serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ZauberDbContext>();
            
            foreach (var file in files)
            {
                if (file.OriginalFile != null)
                {
                    // Set the media id manually so we can create a folder to put it in
                    var mediaId = Guid.NewGuid().NewSequentialGuid();
             
                    // Save the actual file to disk
                    var fileSaveResult = await providerService.StorageProvider!.SaveFile(file.OriginalFile, mediaId.ToString());
                    
                    // Create a media item to save
                    var mediaItem = await providerService.StorageProvider!.ToMedia(fileSaveResult);
                    mediaItem.Id = mediaId;
                    
                    // Save media to db
                    try
                    {
                        dbContext.Media.Add(mediaItem);
                        await dbContext.SaveChangesAsync();
                        fileSaveResult.Success = true;
                    }
                    catch (Exception e)
                    {
                        fileSaveResult.ErrorMessages.Add(e.Message);
                        fileSaveResult.Success = false;
                    }
                    
                    // Add media to save result and clear browser file
                    fileSaveResult.OriginalFile = null;
                    fileSaveResult.SavedMedia = mediaItem;
                    
                    results.Add(fileSaveResult);
                }
                else
                {
                    file.ErrorMessages.Add($"There is no file to upload for {file.Name}");
                    results.Add(file);
                }
            }
        }

        return results;
    }
    
    private async Task<List<FileSaveResult>> SaveMedia(List<Models.Media> media)
    {
        var results = new List<FileSaveResult>();
        
        foreach (var m in media)
        {
            //TODO - Sort the save media stuff
        }

        return results;
    }
}