using ZauberCMS.Core.Content.Interfaces;
using ZauberCMS.Core.Content.Parameters;
using ZauberCMS.Core.Media.Interfaces;
using ZauberCMS.Core.Media.Parameters;
using ZauberCMS.Core.Membership.Interfaces;
using ZauberCMS.Core.Membership.Models;
using ZauberCMS.Core.Membership.Parameters;
using ZauberCMS.Core.Shared.Models;

namespace ZauberCMS.Core.Extensions;

public static class ContentExtensions
{
    
    /// <summary>
    /// Gets the URL for the content with additional processing/validation
    /// </summary>
    /// <param name="content">The content instance</param>
    /// <returns>The processed URL</returns>
    public static string? Url(this Content.Models.Content content)
    {
        // Add your custom logic here
        // For example: URL validation, formatting, etc.
#pragma warning disable CS0618 // Type or member is obsolete
        var url = content.Url; // Direct property access within extension
#pragma warning restore CS0618 // Type or member is obsolete
            
        // Custom processing...
        return url;
    }


    /// <summary>
    /// Gets navigation items making sure picked content is up to date
    /// </summary>
    /// <param name="content"></param>
    /// <param name="alias"></param>
    /// <param name="contentService"></param>
    /// <returns></returns>
    public static async Task<List<NavigationItem>> NavigationItems(this IHasPropertyValues content, string alias, IContentService contentService)
    {
        var navItems = content.GetValue<List<NavigationItem>>(alias);
        if (navItems?.Count > 0)
        {
            await ProcessNavigationItems(navItems, contentService);
        }
        return navItems ?? [];
    }

    private static async Task ProcessNavigationItems(List<NavigationItem> navItems, IContentService contentService)
    {
        // Collect all ContentIds from this level
        var navWithContent = navItems.Where(x => x.ContentId != null).Select(x => x.ContentId!.Value).ToList();
    
        // Process this level's ContentIds
        if (navWithContent.Count > 0)
        {
            var contentItems = await contentService.QueryContentAsync(new QueryContentParameters
            {
                Ids = navWithContent,
                AmountPerPage = navWithContent.Count,
                Cached = true
            });
            var dictContentItems = contentItems.Items.ToDictionary(x => x.Id, x => x);
        
            foreach (var navigationItem in navItems)
            {
                if (navigationItem.ContentId != null)
                {
                    if (dictContentItems.TryGetValue(navigationItem.ContentId.Value, out var newContent))
                    {
                        navigationItem.Url = newContent.Url();
                    }
                }
            
                // Recursively process children if they exist
                if (navigationItem.Children?.Count > 0)
                {
                    await ProcessNavigationItems(navigationItem.Children, contentService);
                }
            }
        }
        // If there are no ContentIds at this level but there might be children to process
        else
        {
            foreach (var navigationItem in navItems)
            {
                if (navigationItem.Children?.Count > 0)
                {
                    await ProcessNavigationItems(navigationItem.Children, contentService);
                }
            }
        }
    }

    
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
    /// Retrieves a collection of content blocks based on the specified alias and associated content IDs.
    /// </summary>
    /// <param name="content">The content instance implementing <see cref="IHasPropertyValues"/>.</param>
    /// <param name="alias">The alias of the property containing the block IDs.</param>
    /// <param name="contentService">The service for querying content blocks.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains a collection of content blocks.</returns>
    public static async Task<IEnumerable<Content.Models.Content>> GetBlocks(this IHasPropertyValues content,
        string alias,
        IContentService contentService)
    {
        // Original efficient path - direct database query with zero overhead
        var ids = content.GetValue<List<Guid>>(alias);
        if (ids != null && ids.Count != 0)
        {
            var blockList =
                await contentService.QueryContentAsync(new QueryContentParameters { Ids = ids, AmountPerPage = 150, NestedFilter = BaseQueryContentParameters.NestedContentFilter.Only});
            // Ensure the returned items are in the same order as the input ids
            var dict = blockList.Items.ToDictionary(x => x.Id, x => x);
            return ids.Where(dict.ContainsKey).Select(id => dict[id]);
        }

        return [];
    }
    
    /// <summary>
    /// Retrieves a collection of content blocks based on the specified alias and associated content IDs.
    /// Optionally includes in-memory pending changes for preview rendering.
    /// </summary>
    /// <param name="content">The content instance implementing <see cref="IHasPropertyValues"/>.</param>
    /// <param name="alias">The alias of the property containing the block IDs.</param>
    /// <param name="contentService">The service for querying content blocks.</param>
    /// <param name="includePendingChanges">If true, checks in-memory pending changes before querying database (used for live previews in admin).</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains a collection of content blocks.</returns>
    public static async Task<IEnumerable<Content.Models.Content>> GetBlocks(this IHasPropertyValues content,
        string alias,
        IContentService contentService,
        bool includePendingChanges)
    {
        // If not checking pending changes, use the original efficient method
        if (!includePendingChanges)
        {
            return await content.GetBlocks(alias, contentService);
        }
        
        // Admin preview path - check for pending changes first
        var ids = content.GetValue<List<Guid>>(alias);
        if (ids == null || ids.Count == 0)
        {
            return [];
        }
        
        // Check if we have pending changes for this property (admin editing only)
        if (content is Content.Models.Content contentModel && 
            contentModel.PendingBlockListChanges.TryGetValue(alias, out var pendingChanges) &&
            pendingChanges.Count > 0)
        {
            // Return items from pending changes in the correct order
            return ids.Where(pendingChanges.ContainsKey).Select(id => pendingChanges[id]);
        }
        
        // No pending changes, fall back to database query
        return await content.GetBlocks(alias, contentService);
    }
    
    /// <summary>
    /// Retrieves a list of media items from a content property.
    /// </summary>
    /// <param name="content">The content item that holds the media property.</param>
    /// <param name="alias">The alias of the media property to fetch.</param>
    /// <param name="mediaService">The mediator to send queries to retrieve media information.</param>
    /// <param name="fallBackUrl">A fallback URL to use if no media is found.</param>
    /// <returns>Returns a list of media items if found; otherwise, returns a list containing a single media item with the fallback URL.</returns>
    public static async Task<List<Media.Models.Media>> GetMediaItems(this IHasPropertyValues content, string? alias,
        IMediaService mediaService, string? fallBackUrl = null)
    {
        if (!string.IsNullOrEmpty(alias))
        {
            var valueString = content.GetValue<string>(alias);
            if (!valueString.IsNullOrWhiteSpace())
            {
                if (valueString.Contains('['))
                {
                    var mediaIds = content.GetValue<List<Guid>>(alias);
                    if (mediaIds != null && mediaIds.Count != 0)
                    {
                        var result = await mediaService.QueryMediaAsync(new QueryMediaParameters { Ids = mediaIds, AmountPerPage = mediaIds.Count, Cached = true });
                        return result.Items.ToList();
                    }                      
                }
                else
                {
                    var mediaId = content.GetValue<Guid>(alias);
                    if (mediaId != Guid.Empty)
                    {
                        var media = await mediaService.GetMediaAsync(new GetMediaParameters {Id = mediaId});
                        if (media != null)
                        {
                            return [media];
                        }
                    }   
                }
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
    /// <param name="contentService"></param>
    /// <returns></returns>
    public static async Task<List<Content.Models.Content>> GetContentItems(this IHasPropertyValues content, string? propertyAlias, IContentService contentService)
    {
        if (!string.IsNullOrEmpty(propertyAlias))
        {
            var valueString = content.GetValue<string>(propertyAlias);
            if (!valueString.IsNullOrWhiteSpace())
            {
                if (valueString.Contains('['))
                {
                    var ids = content.GetValue<List<Guid>>(propertyAlias);
                    if (ids != null && ids.Count != 0)
                    {
                        var result = await contentService.QueryContentAsync(new QueryContentParameters { Ids = ids, AmountPerPage = ids.Count, Cached = true});
                        return result.Items.ToList();
                    }
                }
                else
                {
                    var id = content.GetValue<Guid>(propertyAlias);
                    if (id != Guid.Empty)
                    {
                        var media = await contentService.GetContent(id);
                        if (media != null)
                        {
                            return [media];
                        }
                    }  
                }
            }
        }

        return [];
    }
    
    /// <summary>
    /// Extension to get users
    /// </summary>
    /// <param name="content"></param>
    /// <param name="propertyAlias"></param>
    /// <param name="membershipService"></param>
    /// <returns></returns>
    public static async Task<List<User>> GetUsers(this IHasPropertyValues content, string propertyAlias, IMembershipService membershipService)
    {
        if (!string.IsNullOrEmpty(propertyAlias))
        {
            var ids = content.GetValue<List<Guid>>(propertyAlias);
            if (ids != null && ids.Count != 0)
            {
                var result = await membershipService.QueryUsersAsync(new QueryUsersParameters { Ids = ids, AmountPerPage = ids.Count, Cached = true});
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
    /// <param name="membershipService">The membershipService to handle user queries</param>
    /// <returns>A single user or null if no users are found</returns>
    public static async Task<User?> GetUser(this IHasPropertyValues content, string propertyAlias, IMembershipService membershipService)
    {
        return (await content.GetUsers(propertyAlias, membershipService)).FirstOrDefault();
    }


    /// <summary>
    /// Extension to get a single media item
    /// </summary>
    /// <param name="content">The content containing media data</param>
    /// <param name="propertyAlias">The property alias to retrieve media ids</param>
    /// <param name="mediaService">The mediaService to handle media queries</param>
    /// <param name="fallBackUrl"></param>
    /// <returns>A single media item or null if no media items are found</returns>
    public static async Task<Media.Models.Media?> GetMedia(this IHasPropertyValues content, string propertyAlias, IMediaService mediaService, string? fallBackUrl = null)
    {
        return (await content.GetMediaItems(propertyAlias, mediaService, fallBackUrl)).FirstOrDefault();
    }

    /// <summary>
    /// Extension to get a single content item
    /// </summary>
    /// <param name="content">The content containing content data</param>
    /// <param name="propertyAlias">The property alias to retrieve content ids</param>
    /// <param name="contentService">The content service</param>
    /// <returns>A single content item or null if no content items are found</returns>
    public static async Task<Content.Models.Content?> GetContent(this IHasPropertyValues content, string propertyAlias, IContentService contentService)
    {
        return (await content.GetContentItems(propertyAlias, contentService)).FirstOrDefault();
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