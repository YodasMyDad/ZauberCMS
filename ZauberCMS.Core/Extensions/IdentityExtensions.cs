using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Serilog;
using ZauberCMS.Core.Content.Commands;
using ZauberCMS.Core.Media.Commands;
using ZauberCMS.Core.Membership.Models;

namespace ZauberCMS.Core.Extensions;

public static class IdentityExtensions
{
        /// <summary>
    /// Get a value from a content property
    /// </summary>
    /// <param name="user">User</param>
    /// <param name="alias">The property alias</param>
    /// <typeparam name="T">The type you expect the value in</typeparam>
    /// <returns></returns>
    public static T? GetValue<T>(this User user, string alias)
    {
        return user.ContentValues().TryGetValue(alias, out var contentValue) ? contentValue.Value.ToValue<T>() : default;
    }

    /// <summary>
    /// Gets media items from a picker
    /// </summary>
    /// <param name="user">The User to get the media item from</param>
    /// <param name="alias">The property alias</param>
    /// <param name="mediator">An injected IMediatr</param>
    /// <param name="fallBackUrl">Fallback url in case the media item is null</param>
    /// <returns>List on media</returns>
    public static async Task<IEnumerable<Media.Models.Media?>> GetMedia(this User user, string alias, IMediator mediator, string? fallBackUrl = null)
    {
        var mediaIds = user.GetValue<List<Guid>?>(alias);
        if (mediaIds != null)
        {
            var mediaCount = mediaIds.Count;
            if (mediaCount > 0)
            {
                // TODO - Look at caching these
                var mediaItems=  await mediator.Send(new QueryMediaCommand{Ids = mediaIds, AmountPerPage = mediaCount});
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
    /// <param name="mediator">An injected IMediatr</param>
    /// <returns>List of Content</returns>
    public static async Task<IEnumerable<Content.Models.Content>> GetContent(this User user, string alias, IMediator mediator)
    {
        var contentIds = user.GetValue<List<Guid>?>(alias);
        if (contentIds != null)
        {
            var contentCount = contentIds.Count;
            if (contentCount > 0)
            {
                // TODO - Look at caching these
                var contentItems=  await mediator.Send(new QueryContentCommand{Ids = contentIds, AmountPerPage = contentCount});
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