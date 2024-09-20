using MediatR;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using ZauberCMS.Core.Extensions;
using ZauberCMS.Core.Settings;
using ZauberCMS.Core.Shared.Models;

namespace ZauberCMS.Core.Providers;

public class DiskStorageProvider(
    IWebHostEnvironment env,
    IMediator mediator,
    IOptions<ZauberSettings> settings)
    : IStorageProvider
{
    private readonly ZauberSettings _settings = settings.Value;

    /// <inheritdoc />
    public async Task<HandlerResult<Media.Models.Media>> CanUseFile(IBrowserFile file, bool onlyImages = false)
    {
        var globalSettingsRequest = await mediator.GetGlobalSettings();

        var result = new HandlerResult<Media.Models.Media> { Success = true };

        if (onlyImages && !file.IsImage())
        {
            result.Messages.Add(new ResultMessage
            {
                Message = "File must be an image only",
                MessageType = ResultMessageType.Error
            });
            result.Success = false;
        }
        else
        {
            // Check allowed filetypes
            if (file.Name.Contains(globalSettingsRequest.AllowedFileTypes))
            {
                if (file.Size > globalSettingsRequest.MaxUploadFileSizeInBytes)
                {
                    result.Messages.Add(new ResultMessage
                    {
                        Message = "File is too large",
                        MessageType = ResultMessageType.Error
                    });
                    result.Success = false;
                }
            }
            else
            {
                result.Messages.Add(new ResultMessage
                {
                    Message = "File not allowed",
                    MessageType = ResultMessageType.Error
                });
                result.Success = false;
            }
        }

        return result;
    }

    /// <inheritdoc />
    public Task<bool> DeleteFile(string? url)
    {
        return Task.Run(() =>
        {
            if (!url.IsNullOrWhiteSpace())
            {
                var fullFilePath = Path.Combine(env.WebRootPath, url.Replace("/", "\\"));
                if (File.Exists(fullFilePath))
                {
                    File.Delete(fullFilePath);
                    return true;
                }
            }

            return false;
        });
    }

    /// <inheritdoc />
    public async Task<HandlerResult<Media.Models.Media>> SaveFile(IBrowserFile file,
        Media.Models.Media? existingMedia = null, string? folderName = null, bool overwrite = true)
    {
        var result = await CanUseFile(file);
        if (result.Success)
        {
            // Clear any messages
            result.Messages.Clear();

            try
            {
                var media = new Media.Models.Media();
                if (existingMedia != null)
                {
                    media = existingMedia;
                }

                if (media.Name.IsNullOrEmpty())
                {
                    media.Name = file.Name;
                }

                var relativePath = folderName.IsNullOrWhiteSpace()
                    ? Path.Combine(_settings.UploadFolderName ?? "media", media.Id.ToString())
                    : Path.Combine(_settings.UploadFolderName ?? "media", folderName);

                var dirToSave = Path.Combine(env.WebRootPath, relativePath);
                var di = new DirectoryInfo(dirToSave);
                if (!di.Exists)
                {
                    di.Create();
                }
                
                var globalSettingsRequest = await mediator.GetGlobalSettings();
                var filePath = Path.Combine(dirToSave, file.Name);
                await using (var stream = file.OpenReadStream(globalSettingsRequest.MaxUploadFileSizeInBytes))
                {
                    if (file.IsImage())
                    {
                        using var image = await Image.LoadAsync(stream);
                        image.OverMaxSizeCheck(globalSettingsRequest.MaxImageSizeInPixels);
                        await image.SaveAsync(filePath);
                        media.Width = image.Width;
                        media.Height = image.Height;
                    }
                    else
                    {
                        using var mstream = new MemoryStream();
                        await stream.CopyToAsync(mstream);
                        await File.WriteAllBytesAsync(filePath, mstream.ToArray());
                    }
                }

                media.Url = Path.Combine(relativePath, file.Name).Replace("\\", "/");
                media.FileSize = file.Size;
                media.MediaType = file.Name.ToFileType();
                result.Entity = media;
            }
            catch (Exception ex)
            {
                result.AddMessage(ex.Message, ResultMessageType.Error);
                result.Success = false;
            }

            return result;
        }

        return result;
    }

    /*public Task<Media.Models.Media> ToMedia(FileSaveResult fileSaveResult, Guid? id = null, Guid? parentId = null)
    {
        return Task.Run(() =>
        {
            var mediaItem = new Media.Models.Media
            {
                FileSize = fileSaveResult.FileSize ?? fileSaveResult.OriginalFile?.Size ?? 0,
                Name = fileSaveResult.Name ?? fileSaveResult.OriginalFile?.Name
            };
            if (id != null)
            {
                mediaItem.Id = id.Value;
            }
            if (parentId != null)
            {
                mediaItem.ParentId = parentId.Value;
            }
            mediaItem.Width = fileSaveResult.Width;
            mediaItem.Height = fileSaveResult.Height;
            mediaItem.MediaType = fileSaveResult.MediaType ?? fileSaveResult.OriginalFile?.Name.ToFileType() ?? MediaType.Unknown;
            mediaItem.DateCreated = DateTime.UtcNow;
            mediaItem.Url = fileSaveResult.SavedFileUrl;
            return mediaItem;
        });
    }*/
}