using MediatR;
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
    /// Gets 
    /// </summary>
    /// <param name="content"></param>
    /// <param name="alias"></param>
    /// <param name="mediator"></param>
    /// <returns></returns>
    public static async Task<IEnumerable<Media.Models.Media?>> GetMedia(this Content.Models.Content content, string alias, IMediator mediator)
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