using MediatR;
using ZauberCMS.Core.Content.Commands;
using ZauberCMS.Core.Media.Commands;

namespace ZauberCMS.Core.Extensions;

public static class ContentExtensions
{
    /// <summary>
    /// Get a value from a content property
    /// </summary>
    /// <param name="content">Content item</param>
    /// <param name="alias">The property alias</param>
    /// <typeparam name="T">The type you expect the value in</typeparam>
    /// <returns></returns>
    public static T? GetValue<T>(this Content.Models.Content content, string alias)
    {
        return content.ContentValues().TryGetValue(alias, out var contentValue) ? contentValue.Value.ToValue<T>() : default;
    }

    /// <summary>
    /// Gets media items from a picker
    /// </summary>
    /// <param name="content">The content to get the media item from</param>
    /// <param name="alias">The property alias</param>
    /// <param name="mediator">An injected IMediatr</param>
    /// <param name="fallBackUrl">Fallback url in case the media item is null</param>
    /// <returns>List on media</returns>
    public static async Task<IEnumerable<Media.Models.Media?>> GetMedia(this Content.Models.Content content, string alias, IMediator mediator, string? fallBackUrl = null)
    {
        var mediaIds = content.GetValue<List<Guid>?>(alias);
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
    /// <param name="content">The content to get the media item from</param>
    /// <param name="alias">The property alias</param>
    /// <param name="mediator">An injected IMediatr</param>
    /// <returns>List of Content</returns>
    public static async Task<IEnumerable<Content.Models.Content>> GetContent(this Content.Models.Content content, string alias, IMediator mediator)
    {
        var contentIds = content.GetValue<List<Guid>?>(alias);
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

    private static readonly SlugHelper SlugHelper = new(new SlugHelper.Config
    {
        CharacterReplacements = new Dictionary<string, string> {{" ", ""}}
    });
    
    /// <summary>
    /// Content alias creator
    /// </summary>
    /// <param name="name"></param>
    /// <returns></returns>
    public static string ToAlias(this string? name)
    {
        return name != null ? SlugHelper.GenerateSlug(name) : string.Empty;
    }
}