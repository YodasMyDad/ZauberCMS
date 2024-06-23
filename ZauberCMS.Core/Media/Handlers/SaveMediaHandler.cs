using AutoMapper;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using ZauberCMS.Core.Data;
using ZauberCMS.Core.Extensions;
using ZauberCMS.Core.Media.Commands;
using ZauberCMS.Core.Providers;
using ZauberCMS.Core.Shared.Models;

namespace ZauberCMS.Core.Media.Handlers;

public class SaveMediaHandler(ProviderService providerService, IServiceProvider serviceProvider, IMapper mapper)
    : IRequestHandler<SaveMediaCommand, List<FileSaveResult>>
{
    public async Task<List<FileSaveResult>> Handle(SaveMediaCommand request, CancellationToken cancellationToken)
    {
        var results = new List<FileSaveResult>();
        results.AddRange(await SaveFiles(request.FilesToSave, request.ParentFolderId));
        results.AddRange(await UpdateMedia(request.MediaToSave, request.ParentFolderId));
        return results;
    }

    private async Task<List<FileSaveResult>> SaveFiles(List<FileSaveResult> files, Guid? parentFolderId)
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
                    // Use the existing id if this is an update of an existing media item
                    var isUpdate = file.CurrentMediaId != null;
                    var mediaId = isUpdate ? file.CurrentMediaId : Guid.NewGuid().NewSequentialGuid();

                    // Save the actual file to disk
                    var fileSaveResult =
                        await providerService.StorageProvider!.SaveFile(file.OriginalFile, mediaId.ToString());
                    
                    // Create a media item to save
                    var mediaItem = await providerService.StorageProvider!.ToMedia(fileSaveResult, mediaId, parentFolderId);

                    // Save media to db
                    try
                    {
                        if (isUpdate)
                        {
                            // Get the DB version
                            var dbMedia = dbContext.Media
                                .FirstOrDefault(x => x.Id == mediaItem.Id);
                                // Map the updated properties
                            mapper.Map(mediaItem, dbMedia);
                            if (dbMedia != null) dbMedia.DateUpdated = DateTime.UtcNow;
                            await dbContext.SaveChangesAsync(); ;
                        }
                        else
                        {
                            dbContext.Media.Add(mediaItem);
                            await dbContext.SaveChangesAsync();
                        }
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
            }
        }

        return results;
    }

    private async Task<List<FileSaveResult>> UpdateMedia(List<Models.Media> media, Guid? parentFolderId)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ZauberDbContext>();

        var results = new List<FileSaveResult>();

        foreach (var m in media)
        {
            var fileSaveResult = m.ToFileSaveResult();
            m.ParentId = parentFolderId;
            // Save media to db
            try
            {
                // Get the DB version
                var dbMedia = dbContext.Media
                    .FirstOrDefault(x => x.Id == m.Id);
                
                
                if (dbMedia == null)
                {
                    
                    dbContext.Media.Add(m);
                }
                else
                {
                    // Map the updated properties
                    mapper.Map(m, dbMedia);
                }

                await dbContext.SaveChangesAsync();
                fileSaveResult.Success = true;
            }
            catch (Exception e)
            {
                fileSaveResult.ErrorMessages.Add(e.Message);
                fileSaveResult.Success = false;
            }

            results.Add(fileSaveResult);
        }

        return results;
    }
}