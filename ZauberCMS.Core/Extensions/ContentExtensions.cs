using MediatR;
using ZauberCMS.Core.Content.Commands;
using ZauberCMS.Core.Content.Interfaces;
using ZauberCMS.Core.Media.Commands;
using ZauberCMS.Core.Membership.Commands;
using ZauberCMS.Core.Membership.Models;

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
    public static T? GetValue<T>(this IHasPropertyValues content, string alias)
    {
        return content.ContentValues().TryGetValue(alias, out var contentValue) ? contentValue.ToValue<T>() : default;
    }

    /// <summary>
    /// Retrieves a list of media items from a content property.
    /// </summary>
    /// <param name="content">The content item that holds the media property.</param>
    /// <param name="alias">The alias of the media property to fetch.</param>
    /// <param name="mediator">The mediator to send queries to retrieve media information.</param>
    /// <param name="fallBackUrl">A fallback URL to use if no media is found.</param>
    /// <returns>Returns a list of media items if found; otherwise, returns a list containing a single media item with the fallback URL.</returns>
    public static async Task<List<Media.Models.Media>> GetMedias(this IHasPropertyValues content, string? alias,
        IMediator mediator, string? fallBackUrl = null)
    {
        if (!string.IsNullOrEmpty(alias))
        {
            var mediaIds = content.GetValue<List<Guid>>(alias);
            if (mediaIds != null && mediaIds.Count != 0)
            {
                var result = await mediator.Send(new QueryMediaCommand { Ids = mediaIds, AmountPerPage = mediaIds.Count, Cached = true });
                return result.Items.ToList();
            }   
        }
        
        if (!fallBackUrl.IsNullOrWhiteSpace())
        {
            return [new Media.Models.Media{Name = fallBackUrl, Url = fallBackUrl}];
        }

        
        return [];
    }
    
    /// <summary>
    /// Extension to get content items
    /// </summary>
    /// <param name="content"></param>
    /// <param name="propertyAlias"></param>
    /// <param name="mediator"></param>
    /// <returns></returns>
    public static async Task<List<Content.Models.Content>> GetContents(this IHasPropertyValues content, string? propertyAlias, IMediator mediator)
    {
        if (!string.IsNullOrEmpty(propertyAlias))
        {
            var ids = content.GetValue<List<Guid>>(propertyAlias);
            if (ids != null && ids.Count != 0)
            {
                var result = await mediator.Send(new QueryContentCommand { Ids = ids, AmountPerPage = ids.Count, Cached = true});
                return result.Items.ToList();
            }
        }

        return [];
    }
    
    /// <summary>
    /// Extension to get users
    /// </summary>
    /// <param name="content"></param>
    /// <param name="propertyAlias"></param>
    /// <param name="mediator"></param>
    /// <returns></returns>
    public static async Task<List<User>> GetUsers(this IHasPropertyValues content, string propertyAlias, IMediator mediator)
    {
        if (!string.IsNullOrEmpty(propertyAlias))
        {
            var ids = content.GetValue<List<Guid>>(propertyAlias);
            if (ids != null && ids.Count != 0)
            {
                var result = await mediator.Send(new QueryUsersCommand { Ids = ids, AmountPerPage = ids.Count, Cached = true});
                return result.Items.ToList();
            }
        }

        return [];
    }

    /// <summary>
    /// Extension to get a single user
    /// </summary>
    /// <param name="content">The content containing user data</param>
    /// <param name="propertyAlias">The property alias to retrieve user ids</param>
    /// <param name="mediator">The mediator to handle user queries</param>
    /// <returns>A single user or null if no users are found</returns>
    public static async Task<User?> GetUser(this IHasPropertyValues content, string propertyAlias, IMediator mediator)
    {
        return (await content.GetUsers(propertyAlias, mediator)).FirstOrDefault();
    }
    

    /// <summary>
    /// Extension to get a single media item
    /// </summary>
    /// <param name="content">The content containing media data</param>
    /// <param name="propertyAlias">The property alias to retrieve media ids</param>
    /// <param name="mediator">The mediator to handle media queries</param>
    /// <returns>A single media item or null if no media items are found</returns>
    public static async Task<Media.Models.Media?> GetMedia(this IHasPropertyValues content, string propertyAlias, IMediator mediator)
    {
        return (await content.GetMedias(propertyAlias, mediator)).FirstOrDefault();
    }

    /// <summary>
    /// Extension to get a single content item
    /// </summary>
    /// <param name="content">The content containing content data</param>
    /// <param name="propertyAlias">The property alias to retrieve content ids</param>
    /// <param name="mediator">The mediator to handle content queries</param>
    /// <returns>A single content item or null if no content items are found</returns>
    public static async Task<Content.Models.Content?> GetContent(this IHasPropertyValues content, string propertyAlias, IMediator mediator)
    {
        return (await content.GetContents(propertyAlias, mediator)).FirstOrDefault();
    }

    /// <summary>
    /// Provides methods to generate slugs from given strings, conforming to specific configuration settings.
    /// </summary>
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