using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.WebUtilities;

namespace ZauberCMS.Core.Extensions;

/// <summary>
/// Gets querystring values from a blazor page
/// </summary>
public static class NavigationManagerExtensions
{
    /// <summary>
    /// The current address as the page request's middlewares see it, for
    /// <c>IContentService.GetContentFromRequestAsync</c>: scheme, host and port, then the path below the site's base
    /// address (the request's <c>PathBase</c> is left out, as in <c>HttpRequest.Path</c>), then the query string.
    /// A site under <c>/site</c> at <c>https://example.com/site/fr/about</c> gives <c>https://example.com/fr/about</c>,
    /// so a domain with a path (<c>example.com/fr</c>) is matched the same in a circuit, in <c>EntryPage</c> and in
    /// the middlewares (ZA-190).
    /// </summary>
    /// <param name="navigation">The circuit's or page's navigation manager.</param>
    /// <param name="uri">An absolute address under the base address; null for the current one.</param>
    public static string GetContentRequestUrl(this NavigationManager navigation, string? uri = null)
    {
        ArgumentNullException.ThrowIfNull(navigation);
        var authority = new Uri(navigation.BaseUri).GetLeftPart(UriPartial.Authority);
        return authority + "/" + navigation.ToBaseRelativePath(uri ?? navigation.Uri);
    }

    public static bool TryGetQueryString<T>(this NavigationManager navManager, string key, out T value)
    {
        var uri = navManager.ToAbsoluteUri(navManager.Uri);

        if (QueryHelpers.ParseQuery(uri.Query).TryGetValue(key, out var valueFromQueryString))
        {
            if (typeof(T) == typeof(int) && int.TryParse(valueFromQueryString, out var valueAsInt))
            {
                value = (T)(object)valueAsInt;
                return true;
            }

            if (typeof(T) == typeof(string))
            {
                value = (T)(object)valueFromQueryString.ToString();
                return true;
            }

            if (typeof(T) == typeof(decimal) && decimal.TryParse(valueFromQueryString, out var valueAsDecimal))
            {
                value = (T)(object)valueAsDecimal;
                return true;
            }

            if (typeof(T) == typeof(bool) && bool.TryParse(valueFromQueryString, out var valueAsBool))
            {
                value = (T)(object)valueAsBool;
                return true;
            }
        }

        value = default!;
        return false;
    }
}