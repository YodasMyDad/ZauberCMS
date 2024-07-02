using Microsoft.AspNetCore.Components.Forms;
using ZauberCMS.Core.Shared.Models;

namespace ZauberCMS.Core.Providers;

public interface IStorageProvider
{
    /// <summary>
    /// Saves the file
    /// </summary>
    /// <param name="browserFile"></param>
    /// <param name="existingMedia"></param>
    /// <param name="parentFolderName"></param>
    /// <param name="overwrite"></param>
    public Task<HandlerResult<Media.Models.Media>> SaveFile(IBrowserFile browserFile, Media.Models.Media? existingMedia = null, string? parentFolderName = null, bool overwrite = true);
    
    /// <summary>
    /// Deletes the file
    /// </summary>
    /// <param name="url"></param>
    public Task<bool> DeleteFile(string? url);

    /// <summary>
    /// Checks to determine if a file can be used
    /// </summary>
    /// <param name="browserFile"></param>
    /// <param name="onlyImages"></param>
    public Task<HandlerResult<Media.Models.Media>> CanUseFile(IBrowserFile browserFile, bool onlyImages = false);
    
}