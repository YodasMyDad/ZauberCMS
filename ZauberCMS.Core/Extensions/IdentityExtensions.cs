using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Serilog;
using ZauberCMS.Core.Content.Interfaces;
using ZauberCMS.Core.Content.Parameters;
using ZauberCMS.Core.Media.Interfaces;
using ZauberCMS.Core.Media.Parameters;
using ZauberCMS.Core.Membership.Models;

namespace ZauberCMS.Core.Extensions;

public static class IdentityExtensions
{
    /// <summary>
    /// Gets media items from a picker
    /// </summary>
    /// <param name="user">The User to get the media item from</param>
    /// <param name="alias">The property alias</param>
    /// <param name="mediaService">An injected mediaService</param>
    /// <param name="fallBackUrl">Fallback url in case the media item is null</param>
    /// <returns>List on media</returns>
    public static async Task<IEnumerable<Media.Models.Media?>> GetMedia(this User user, string alias, IMediaService mediaService, string? fallBackUrl = null)
    {
        var mediaIds = user.GetValue<List<Guid>?>(alias);
        if (mediaIds != null)
        {
            var mediaCount = mediaIds.Count;
            if (mediaCount > 0)
            {
                // TODO - Look at caching these
                var mediaItems=  await mediaService.QueryMediaAsync(new QueryMediaParameters {Ids = mediaIds, AmountPerPage = mediaCount});
                return mediaItems.Items;
            }
        }

        if (!fallBackUrl.IsNullOrWhiteSpace())
        {
            return [new Media.Models.Media{Name = fallBackUrl, Url = fallBackUrl}];
        }
        
        return [];
    }
    
    /// <summary>
    /// Gets content items from a picker
    /// </summary>
    /// <param name="user">The User to get the media item from</param>
    /// <param name="alias">The property alias</param>
    /// <param name="contentService">An injected contentService</param>
    /// <returns>List of Content</returns>
    public static async Task<IEnumerable<Content.Models.Content>> GetContent(this User user, string alias, IContentService contentService)
    {
        var contentIds = user.GetValue<List<Guid>?>(alias);
        if (contentIds != null)
        {
            var contentCount = contentIds.Count;
            if (contentCount > 0)
            {
                // TODO - Look at caching these
                var contentItems=  await contentService.QueryContentAsync(new QueryContentParameters {Ids = contentIds, AmountPerPage = contentCount});
                return contentItems.Items;
            }
        }

        return [];
    }
    
    public static void LogErrors<T>(this IdentityResult identityResult, ILogger<T> logger)
    {
        foreach (var identityResultError in identityResult.Errors)
        {
            logger.LogError(identityResultError.Description);
        }
    }

    public static void LogErrors(this IdentityResult identityResult)
    {
        foreach (var identityResultError in identityResult.Errors)
        {
            Log.Error(identityResultError.Description);
        }
    }

    public static IEnumerable<string> ToErrorsList(this IdentityResult identityResult)
    {
        return identityResult.Errors.Select(x => x.Description);
    }
}