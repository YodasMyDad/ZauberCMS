namespace ZauberCMS.Core.Content.Models;

/// <summary>What an <see cref="Interfaces.IContentFinder"/> found: the page to show, and what the view needs to know.</summary>
public sealed class ContentFinderResult
{
    /// <param name="contentId">The page to show. ZauberCMS loads it itself, so only a live page is served.</param>
    public ContentFinderResult(Guid contentId)
    {
        ContentId = contentId;
    }

    /// <param name="content">The page to show (its id is used; the page is loaded again).</param>
    public ContentFinderResult(Content content) : this(content.Id)
    {
    }

    /// <summary>The page to show.</summary>
    public Guid ContentId { get; }

    /// <summary>
    /// The language to show the page in (an ISO code such as <c>fr-FR</c>). Null uses the page's own: its domain's,
    /// its own or its nearest ancestor's, else the site's default language.
    /// </summary>
    public string? LanguageIsoCode { get; init; }

    /// <summary>
    /// Values for the view, read from <see cref="EntryModel.RouteValues"/> (for example the SKU of
    /// <c>/products/{sku}</c>).
    /// </summary>
    public IReadOnlyDictionary<string, object?> RouteValues { get; init; } = new Dictionary<string, object?>();
}
