namespace ZauberCMS.Core.Content.Parameters;

/// <summary>What <c>IContentService.GetContentFromRequestAsync</c> looks up: a front-end address.</summary>
public class GetContentFromRequestParameters
{
    /// <summary>The path below the site's base address (the <c>slug</c> route value); null or empty for the home page.</summary>
    public string? Slug { get; set; }

    /// <summary>True for the home page (the domain's root page, else the first root page).</summary>
    public bool IsRootContent { get; set; }

    /// <summary>Also loads the page's live children.</summary>
    public bool IncludeChildren { get; set; }

    public bool IgnoreInternalRedirect { get; set; }

    /// <summary>
    /// The full address, which picks the domain: scheme, host and port, then the path without the site's
    /// <c>PathBase</c> (as <c>HttpRequest.Path</c>), with or without the query string. In a component use
    /// <c>NavigationManager.GetContentRequestUrl()</c>, which leaves the path base out (ZA-190). Null matches no
    /// domain. Only the domain it is on is part of the cache key, not the host or spelling (ZA-187).
    /// </summary>
    public string? Url { get; set; }
}