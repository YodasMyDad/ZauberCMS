namespace ZauberCMS.Core.Content.Models;

/// <summary>
/// The page a front-end address shows, with its language. <c>EntryPage</c> cascades it to the view, so a view can
/// read it with <c>[CascadingParameter] public EntryModel? Entry { get; set; }</c>.
/// </summary>
public class EntryModel
{
    public Content? Content { get; set; }
    public Dictionary<string, string> LanguageKeys { get; set; } = [];
    public string? LanguageIsoCode { get; set; }

    /// <summary>
    /// The full name of the <see cref="Interfaces.IContentFinder"/> that found <see cref="Content"/>, or null when
    /// the page has the address's URL.
    /// </summary>
    public string? ContentFinder { get; set; }

    /// <summary>The values the content finder gave the view (<see cref="ContentFinderResult.RouteValues"/>); empty otherwise.</summary>
    public IReadOnlyDictionary<string, object?> RouteValues { get; set; } = new Dictionary<string, object?>();
}
