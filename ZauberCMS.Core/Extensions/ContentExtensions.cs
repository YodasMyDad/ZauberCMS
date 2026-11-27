using ZauberCMS.Core.Content.Interfaces;
using ZauberCMS.Core.Content.Mapping;
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
    /// The page's stored URL: the same as <see cref="Content.Models.Content.Url"/>, which it only returns.
    /// </summary>
    [Obsolete("Read the Content.Url property, which this returns unchanged, or ContentUrl.SiteRelative(content) for " +
              "the link (/blog). Url() will be removed in a later major version (Roadmap D57).")]
    public static string? Url(this Content.Models.Content content) => content.Url;


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
                        navigationItem.Url = newContent.Url;
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
        var rawIds = content.GetValue<List<Guid>>(alias);
        if (rawIds == null || rawIds.Count == 0) return [];

        // Filter empty Guids and dedupe; ToDictionary below would otherwise throw on duplicates,
        // and an empty Guid would never match a real content row anyway.
        var ids = rawIds
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToList();

        if (ids.Count == 0) return [];

        var blockList =
            await contentService.QueryContentAsync(new QueryContentParameters
            {
                Ids = ids,
                AmountPerPage = ids.Count, // Don't truncate large block lists
                NestedFilter = BaseQueryContentParameters.NestedContentFilter.Only,
                // Every page view renders its block lists: cached under the Content prefix, which every content
                // save clears (ZA-057). The items are shared: don't change them.
                Cached = true
            });
        // Ensure the returned items are in the same order as the input ids
        var dict = blockList.Items.ToDictionary(x => x.Id, x => x);
        return ids.Where(dict.ContainsKey).Select(id => dict[id]);
    }
    
    /// <summary>
    /// Retrieves a collection of content blocks based on the specified alias and associated content IDs.
    /// Optionally includes in-memory pending changes for preview rendering.
    /// </summary>
    /// <param name="content">The content instance implementing <see cref="IHasPropertyValues"/>.</param>
    /// <param name="alias">The alias of the property containing the block IDs.</param>
    /// <param name="contentService">The service for querying content blocks.</param>
    /// <param name="includePendingChanges">For admin previews: the blocks as the editor shows them. Unsaved blocks
    /// in <c>PendingBlockListChanges</c> win; the others are read with their pending draft, unpublished ones included.</param>
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
        var rawIds = content.GetValue<List<Guid>>(alias);
        if (rawIds == null || rawIds.Count == 0)
        {
            return [];
        }

        var ids = rawIds.Where(id => id != Guid.Empty).Distinct().ToList();
        if (ids.Count == 0) return [];

        // Unsaved blocks the admin editor put on this copy win; the others come from the database, as the editor
        // shows them (with a pending draft from "Save only", unpublished ones included)
        var pendingChanges = content is Content.Models.Content contentModel &&
                             contentModel.PendingBlockListChanges.TryGetValue(alias, out var pending)
            ? pending
            : new Dictionary<Guid, Content.Models.Content>();

        var storedIds = ids.Where(id => !pendingChanges.ContainsKey(id)).ToList();
        var stored = new Dictionary<Guid, Content.Models.Content>();
        if (storedIds.Count > 0)
        {
            // Unpublished blocks too: the preview shows what the editor shows
            var result = await contentService.QueryContentAsync(new QueryContentParameters
            {
                Ids = storedIds,
                AmountPerPage = storedIds.Count,
                IncludeUnpublished = true,
                NestedFilter = BaseQueryContentParameters.NestedContentFilter.Only
            });
            // A block with a pending draft shows the draft, as in the editor (on a copy: the result may be cached)
            stored = result.Items.ToDictionary(x => x.Id, x => x.UnpublishedContent == null
                ? x
                : x.UnpublishedContent.JsonContent.MapBlockDraftTo(x.MapToNew()));
        }

        return ids.Select(id => pendingChanges.TryGetValue(id, out var unsaved) ? unsaved
                : stored.GetValueOrDefault(id))
            .Where(x => x != null)
            .Select(x => x!)
            .ToList();
    }
    
    /// <summary>
    /// Retrieves a list of media items from a content property.
    /// </summary>
    /// <param name="content">The content item that holds the media property.</param>
    /// <param name="alias">The alias of the media property to fetch.</param>
    /// <param name="mediaService">The mediator to send queries to retrieve media information.</param>
    /// <param name="fallBackUrl">A fallback URL to use if no media is found.</param>
    /// <returns>The media in the order the editor chose (recycled media left out); when there is none, a list holding
    /// one item with the fallback URL, or an empty list.</returns>
    public static async Task<List<Media.Models.Media>> GetMediaItems(this IHasPropertyValues content, string? alias,
        IMediaService mediaService, string? fallBackUrl = null)
    {
        if (!string.IsNullOrEmpty(alias))
        {
            var mediaIds = content.GetValue<string>(alias).ToPickerIds();
            if (mediaIds.Count != 0)
            {
                // Cached: every page view reads its pickers (ZA-057)
                var result = await mediaService.QueryMediaAsync(new QueryMediaParameters
                    { Ids = mediaIds, AmountPerPage = mediaIds.Count, Cached = true });
                var media = result.Items.InPickerOrder(mediaIds, x => x.Id);
                if (media.Count != 0)
                {
                    return media;
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
    /// The pages a content picker holds, in the order the editor chose. Only published pages that are not in the
    /// recycle bin, as a visitor would see them.
    /// </summary>
    public static async Task<List<Content.Models.Content>> GetContentItems(this IHasPropertyValues content, string? propertyAlias, IContentService contentService)
    {
        if (!string.IsNullOrEmpty(propertyAlias))
        {
            var ids = content.GetValue<string>(propertyAlias).ToPickerIds();
            if (ids.Count != 0)
            {
                var result = await contentService.QueryContentAsync(new QueryContentParameters
                    { Ids = ids, AmountPerPage = ids.Count, Cached = true });
                return result.Items.InPickerOrder(ids, x => x.Id);
            }
        }

        return [];
    }

    /// <summary>The users a property holds, in stored order.</summary>
    public static async Task<List<User>> GetUsers(this IHasPropertyValues content, string propertyAlias, IMembershipService membershipService)
    {
        if (!string.IsNullOrEmpty(propertyAlias))
        {
            var ids = content.GetValue<string>(propertyAlias).ToPickerIds();
            if (ids.Count != 0)
            {
                var result = await membershipService.QueryUsersAsync(new QueryUsersParameters { Ids = ids, AmountPerPage = ids.Count, Cached = true});
                return result.Items.InPickerOrder(ids, x => x.Id);
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