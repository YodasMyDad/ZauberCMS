using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Options;
using ZauberCMS.Core.Extensions;
using ZauberCMS.Core.Media.Models;
using ZauberCMS.Core.Settings;
using ZauberCMS.Core.Shared.Models;

namespace ZauberCMS.Core.Providers;

public class DiskStorageProvider(
    IWebHostEnvironment env,
    IOptions<ZauberSettings> settings)
    : IStorageProvider
{
    private readonly ZauberSettings _settings = settings.Value;

        /// <inheritdoc />
        public Task<FileSaveResult> CanUseFile(IBrowserFile file, bool onlyImages = false)
        {
            return Task.Run(() =>
            {
                var fileSaveResult = new FileSaveResult();

                if (onlyImages && !file.IsImage())
                {
                    fileSaveResult.ErrorMessages.Add("File must be an image only");
                    fileSaveResult.Success = false;
                }
                else
                {
                    // Check allowed filetypes
                    if (file.Name.Contains(_settings.AllowedFileTypes))
                    {
                        if (file.Size > _settings.MaxUploadFileSizeInBytes)
                        {
                            fileSaveResult.ErrorMessages.Add("File is too large");
                            fileSaveResult.Success = false;
                        }
                    }
                    else
                    {
                        fileSaveResult.ErrorMessages.Add("File not allowed");
                        fileSaveResult.Success = false;
                    }

                }
                fileSaveResult.MediaType = file.Name.ToFileType();
                fileSaveResult.Name = file.Name;
                fileSaveResult.OriginalFile = file;
                return fileSaveResult;
            });
        }

        /// <inheritdoc />
        public Task<bool> DeleteFile(string? url, string? fileId = null)
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
        public async Task<FileSaveResult> SaveFile(IBrowserFile file, string? folderName = null, bool overwrite = true)
        {
            var fileSaveResult = await CanUseFile(file);
            fileSaveResult.OriginalFile = file;
            if (!fileSaveResult.Success)
            {
                return fileSaveResult;
            }

            try
            {
                var relativePath = folderName.IsNullOrWhiteSpace() ? 
                    Path.Combine(_settings.UploadFolderName ?? "media") : 
                    Path.Combine(_settings.UploadFolderName ?? "media", folderName);
                
                var dirToSave = Path.Combine(env.WebRootPath, relativePath);
                var di = new DirectoryInfo(dirToSave);
                if (!di.Exists)
                {
                    di.Create();
                }
                var filePath = Path.Combine(dirToSave, file.Name);
                await using (var stream = file.OpenReadStream(_settings.MaxUploadFileSizeInBytes))
                {
                    if (file.IsImage())
                    {
                        using var image = await Image.LoadAsync(stream);
                        image.OverMaxSizeCheck(_settings.MaxImageSizeInPixels);
                        await image.SaveAsync(filePath);
                        fileSaveResult.Width = image.Width;
                        fileSaveResult.Height = image.Height;
                    }
                    else
                    {
                        using var mstream = new MemoryStream();
                        await stream.CopyToAsync(mstream);
                        await File.WriteAllBytesAsync(filePath, mstream.ToArray());
                    }
                }
                fileSaveResult.SavedFileUrl = Path.Combine(relativePath, file.Name).Replace("\\", "/");
            }
            catch (Exception ex)
            {
                fileSaveResult.ErrorMessages.Add(ex.Message);
                fileSaveResult.Success = false;
            }

            return fileSaveResult;
        }

    public Task<Media.Models.Media> ToMedia(FileSaveResult fileSaveResult)
    {
        return Task.Run(() =>
        {
            var mediaItem = new Media.Models.Media
            {
                FileSize = fileSaveResult.OriginalFile?.Size ?? 0,
                Name = fileSaveResult.OriginalFile?.Name
            };
            if (fileSaveResult.OriginalFile?.IsImage() == true)
            {
                mediaItem.Width = fileSaveResult.Width;
                mediaItem.Height = fileSaveResult.Height;
            }
            mediaItem.MediaType = fileSaveResult.OriginalFile?.Name.ToFileType() ?? MediaType.Unknown;
            mediaItem.DateCreated = DateTime.UtcNow;
            mediaItem.Url = fileSaveResult.SavedFileUrl;
            return mediaItem;
        });
    }
}