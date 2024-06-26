using Microsoft.AspNetCore.Components.Forms;
using ZauberCMS.Core.Shared.Models;

namespace ZauberCMS.Core.Providers;

public interface IStorageProvider
{
    /// <summary>
    /// Saves the file
    /// </summary>
    /// <param name="browserFile"></param>
    /// <param name="fileName"></param>
    /// <param name="folderName"></param>
    /// <param name="overwrite"></param>
    public Task<FileSaveResult> SaveFile(IBrowserFile browserFile, string? fileName = null, string? folderName = null, bool overwrite = true);
    
    /// <summary>
    /// Deletes the file
    /// </summary>
    /// <param name="url"></param>
    /// <param name="fileId"></param>
    public Task<bool> DeleteFile(string? url, string? fileId = null);

    /// <summary>
    /// Checks to determine if a file can be used
    /// </summary>
    /// <param name="browserFile"></param>
    /// <param name="fileName"></param>
    /// <param name="onlyImages"></param>
    public Task<FileSaveResult> CanUseFile(IBrowserFile browserFile, string? fileName = null, bool onlyImages = false);

    /// <summary>
    /// Method to convert a saved file into a BlogFodderFile object
    /// </summary>
    /// <param name="fileSaveResult"></param>
    /// <param name="id"></param>
    /// <param name="parentId"></param>
    /// <returns></returns>
    public Task<Media.Models.Media> ToMedia(FileSaveResult fileSaveResult, Guid? id = null, Guid? parentId = null);
}