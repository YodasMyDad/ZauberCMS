using System.Text.RegularExpressions;
using ImageResize.Core.Extensions;
using ImageResize.Core.Interfaces;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using ZauberCMS.Core.Data.Interfaces;
using ZauberCMS.Core.Extensions;
using ZauberCMS.Core.Settings;
using ZauberCMS.Core.Shared.Models;

namespace ZauberCMS.Core.Providers;

public class DiskStorageProvider(
    IWebHostEnvironment env,
    IDataService dataService,
    IOptions<ZauberSettings> settings,
    IImageResizerService imageResizerService)
    : IStorageProvider
{
    private readonly ZauberSettings _settings = settings.Value;

    /// <inheritdoc />
    public async Task<HandlerResult<Media.Models.Media>> CanUseFile(IBrowserFile file, bool onlyImages = false)
    {
        var globalSettingsRequest = await dataService.GetGlobalSettings();

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
            var fileExtension = Path.GetExtension(file.Name).ToLower();
            if (globalSettingsRequest.AllowedFileTypes.Contains(fileExtension))
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
                else if (!await MagicBytesMatchExtension(file, fileExtension, globalSettingsRequest.MaxUploadFileSizeInBytes))
                {
                    result.Messages.Add(new ResultMessage
                    {
                        Message = "File contents do not match the file extension",
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

    // Defence in depth on top of the extension allow-list. Extensions are trivially spoofed
    // ("evil.html" renamed to "evil.png"), so we sniff the first bytes of the upload and
    // confirm they match the declared extension. We only reject when we have a positive
    // mismatch — extensions without well-known signatures (.txt, .svg if re-enabled) pass
    // through unchanged so the allow-list remains the authoritative gate for those.
    private static async Task<bool> MagicBytesMatchExtension(IBrowserFile file, string extension, long maxBytes)
    {
        var expected = MagicBytesFor(extension);
        if (expected.Count == 0)
        {
            return true;
        }

        var maxSignatureLength = expected.Max(s => s.Length);
        var buffer = new byte[maxSignatureLength];
        int read;
        try
        {
            await using var stream = file.OpenReadStream(maxBytes);
            read = await stream.ReadAtLeastAsync(buffer, maxSignatureLength, throwOnEndOfStream: false);
        }
        catch
        {
            return false;
        }

        if (read < expected.Min(s => s.Length))
        {
            return false;
        }

        return expected.Any(sig => sig.Length <= read && buffer.AsSpan(0, sig.Length).SequenceEqual(sig));
    }

    private static IReadOnlyList<byte[]> MagicBytesFor(string extension) => extension switch
    {
        ".jpg" or ".jpeg" => [[0xFF, 0xD8, 0xFF]],
        ".png" => [[0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]],
        ".gif" => [[0x47, 0x49, 0x46, 0x38, 0x37, 0x61], [0x47, 0x49, 0x46, 0x38, 0x39, 0x61]],
        ".webp" => [[0x52, 0x49, 0x46, 0x46]], // "RIFF" — full check would also verify "WEBP" at offset 8
        ".ico" => [[0x00, 0x00, 0x01, 0x00]],
        ".pdf" => [[0x25, 0x50, 0x44, 0x46]], // "%PDF"
        ".mp4" => [
            [0x00, 0x00, 0x00, 0x18, 0x66, 0x74, 0x79, 0x70],
            [0x00, 0x00, 0x00, 0x20, 0x66, 0x74, 0x79, 0x70],
            [0x00, 0x00, 0x00, 0x1C, 0x66, 0x74, 0x79, 0x70]
        ],
        ".webm" => [[0x1A, 0x45, 0xDF, 0xA3]],
        ".mp3" => [[0x49, 0x44, 0x33], [0xFF, 0xFB], [0xFF, 0xF3], [0xFF, 0xF2]],
        ".wav" => [[0x52, 0x49, 0x46, 0x46]], // "RIFF"
        ".ogg" => [[0x4F, 0x67, 0x67, 0x53]],
        _ => []
    };

    /// <inheritdoc />
    public Task<bool> DeleteFile(string? url)
    {
        return Task.Run(() =>
        {
            if (url.IsNullOrWhiteSpace())
            {
                return false;
            }

            if (!TryResolveUnderWebRoot(url, out var fullFilePath))
            {
                return false;
            }

            if (File.Exists(fullFilePath))
            {
                File.Delete(fullFilePath);
                return true;
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

                if (!folderName.IsNullOrWhiteSpace() && !IsSafeFolderSegment(folderName))
                {
                    result.AddMessage("Invalid folder name", ResultMessageType.Error);
                    result.Success = false;
                    return result;
                }

                var relativePath = folderName.IsNullOrWhiteSpace()
                    ? Path.Combine(_settings.UploadFolderName ?? "media", media.Id.ToString())
                    : Path.Combine(_settings.UploadFolderName ?? "media", folderName);

                if (!TryResolveUnderWebRoot(relativePath, out var dirToSave))
                {
                    result.AddMessage("Invalid upload path", ResultMessageType.Error);
                    result.Success = false;
                    return result;
                }

                var di = new DirectoryInfo(dirToSave);
                if (!di.Exists)
                {
                    di.Create();
                }
                
                // Sanitize the filename for URL safety while preserving the extension
                var sanitizedFileName = SanitizeFileName(file.Name);
                
                var globalSettingsRequest = await dataService.GetGlobalSettings();
                var filePath = Path.Combine(dirToSave, sanitizedFileName);
                await using (var stream = file.OpenReadStream(globalSettingsRequest.MaxUploadFileSizeInBytes))
                {
                    if (file.IsImage())
                    {
                        using var image = await stream.OverMaxSizeCheckAsync(globalSettingsRequest.MaxImageSizeInPixels, imageResizerService);
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

                media.Url = Path.Combine(relativePath, sanitizedFileName).Replace("\\", "/");
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

    private bool TryResolveUnderWebRoot(string relativeOrUrlPath, out string fullPath)
    {
        var rootFull = Path.GetFullPath(env.WebRootPath);
        var rootWithSep = rootFull.EndsWith(Path.DirectorySeparatorChar)
            ? rootFull
            : rootFull + Path.DirectorySeparatorChar;

        var normalised = relativeOrUrlPath.TrimStart('/', '\\').Replace('/', Path.DirectorySeparatorChar);

        try
        {
            var combined = Path.Combine(rootFull, normalised);
            fullPath = Path.GetFullPath(combined);
        }
        catch
        {
            fullPath = string.Empty;
            return false;
        }

        return fullPath.StartsWith(rootWithSep, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSafeFolderSegment(string segment)
    {
        if (string.IsNullOrWhiteSpace(segment)) return false;
        if (segment.Contains("..")) return false;
        if (Path.IsPathRooted(segment)) return false;
        if (segment.IndexOfAny(['/', '\\', ':', '*', '?', '"', '<', '>', '|']) >= 0) return false;
        if (segment.Any(char.IsControl)) return false;
        return true;
    }

    /// <summary>
    /// Sanitizes a filename to be URL-safe by replacing spaces and special characters.
    /// Preserves the file extension.
    /// </summary>
    /// <param name="fileName">The original filename</param>
    /// <returns>A sanitized filename safe for URLs</returns>
    private static string SanitizeFileName(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return fileName;
        }

        // Get the extension and name separately
        var extension = Path.GetExtension(fileName);
        var nameWithoutExtension = Path.GetFileNameWithoutExtension(fileName);

        // Replace spaces with hyphens
        nameWithoutExtension = nameWithoutExtension.Replace(" ", "-");

        // Remove or replace characters that are not alphanumeric, hyphens, or underscores
        // Keep dots for versioned filenames like "file.v2.txt"
        nameWithoutExtension = Regex.Replace(nameWithoutExtension, @"[^a-zA-Z0-9\-_\.]", "-");

        // Replace multiple consecutive hyphens with a single hyphen
        nameWithoutExtension = Regex.Replace(nameWithoutExtension, @"-+", "-");

        // Remove leading and trailing hyphens
        nameWithoutExtension = nameWithoutExtension.Trim('-');

        // Reconstruct the filename with the extension
        return $"{nameWithoutExtension}{extension}";
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