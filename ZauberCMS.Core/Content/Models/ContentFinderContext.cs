namespace ZauberCMS.Core.Content.Models;

/// <summary>The address an <see cref="Interfaces.IContentFinder"/> is asked about.</summary>
public sealed class ContentFinderContext
{
    /// <summary>
    /// The address as the caller gave it, with its query string: the full request URL for a page request, or null
    /// when code looked a slug up without one.
    /// </summary>
    public string? Url { get; init; }

    /// <summary>The path in the stored URL form (<c>ContentUrl.Normalize</c>): lower case, no slashes around it, empty for the home page.</summary>
    public required string Slug { get; init; }

    /// <summary>
    /// The path as asked for, in its own case, without slashes around it (<c>Products/ABC-123</c>); empty for the
    /// home page. For values whose case matters, such as a SKU; <see cref="Slug"/> is what page URLs are compared with.
    /// </summary>
    public string Path { get; init; } = string.Empty;

    /// <summary>The culture domain the address matched (Settings → Domains), or null.</summary>
    public Domain? Domain { get; init; }

    /// <summary>True when the address is the home page (an empty slug).</summary>
    public bool IsRootContent => Slug.Length == 0;

    /// <summary>The segments of <see cref="Slug"/>, lower case (<c>products/abc-123</c> → <c>products</c>, <c>abc-123</c>).</summary>
    public string[] Segments => Slug.Length == 0 ? [] : Slug.Split('/', StringSplitOptions.RemoveEmptyEntries);
}
