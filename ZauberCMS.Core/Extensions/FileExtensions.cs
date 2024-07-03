using Microsoft.AspNetCore.Components.Forms;
using ZauberCMS.Core.Data;
using ZauberCMS.Core.Media.Models;
using ZauberCMS.Core.Providers;
using ZauberCMS.Core.Shared.Models;

namespace ZauberCMS.Core.Extensions;

    public static class FileExtensions
    {
        /*public static async Task<Media.Models.Media?> AddFileToDb<T>(this IBrowserFile browserFile, Guid id,
            HandlerResult<T> result,
            ProviderService providerService, ZauberDbContext dbContext)
        {
            return await browserFile.AddMediaToDb(id.ToString(), result, providerService, dbContext);
        }

        /// <summary>
        /// Saves the BrowserFile as a Media using the set StorageProvider
        /// </summary>
        /// <param name="browserFile"></param>
        /// <param name="id"></param>
        /// <param name="result"></param>
        /// <param name="providerService"></param>
        /// <param name="dbContext"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public static async Task<Media.Models.Media?> AddMediaToDb<T>(this IBrowserFile browserFile, string id, HandlerResult<T> result, 
            ProviderService providerService, ZauberDbContext dbContext)
        {
            var fileSaveResult = await providerService.StorageProvider!.SaveFile(browserFile, null, id);
            if (!fileSaveResult.Success)
            {
                foreach (var errorMessage in fileSaveResult.ErrorMessages)
                {
                    result.AddMessage(errorMessage, ResultMessageType.Warning);
                }

                return null;
            }

            // Create the file
            var file = await providerService.StorageProvider.ToMedia(fileSaveResult);

            // Save the file first
            dbContext.Medias.Add(file);

            // Set the file to the user
            return file;
        }*/
        
        /// <summary>
        /// Is this file a video based on the extension
        /// </summary>
        /// <param name="file"></param>
        /// <returns></returns>
        public static bool IsVideo(this IBrowserFile file)
        {
            return file.Name.IsVideo();
        }

        /// <summary>
        /// Is this file a video based on the extension
        /// </summary>
        /// <param name="fileName"></param>
        /// <returns></returns>
        public static bool IsVideo(this string fileName)
        {
            var mediaType = fileName.GetMediaTypeFromUrl();
            return mediaType == MediaType.Video;
        }

        /// <summary>
        /// Is file an image
        /// </summary>
        /// <param name="fileName"></param>
        /// <returns></returns>
        public static bool IsImage(this string fileName)
        {
            var mediaType = fileName.GetMediaTypeFromUrl();
            return mediaType == MediaType.Image;
        }

        /// <summary>
        /// Is file an audio file
        /// </summary>
        /// <param name="fileName"></param>
        /// <returns></returns>
        public static bool IsAudio(this string fileName)
        {
            var mediaType = fileName.GetMediaTypeFromUrl();
            return mediaType == MediaType.Audio;
        }
        
        /// <summary>
        /// Is the file an image
        /// </summary>
        /// <param name="browserFile"></param>
        /// <returns></returns>
        public static bool IsImage(this IBrowserFile browserFile)
        {
            return browserFile.Name.IsImage();
        }

        /// <summary>
        /// Returns a file type from the film name
        /// </summary>
        /// <param name="fileName"></param>
        /// <returns></returns>
        public static MediaType ToFileType(this string fileName)
        {
            return fileName.GetMediaTypeFromUrl();
        }
    }