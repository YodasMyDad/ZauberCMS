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

    /// <summary>
    /// A return address that is safe to send the visitor back to: a path from the host's root (starting with <c>/</c>,
    /// the site's path base and the query string kept), or null for an empty one, an absolute one that is not below
    /// this site's base address, or one a browser reads as another host (<c>//host</c>, <c>/\host</c>). For the
    /// <c>returnUrl</c> a login link carries (ZA-201).
    /// </summary>
    public static string? ToLocalReturnUrl(this NavigationManager navigation, string? returnUrl)
    {
        ArgumentNullException.ThrowIfNull(navigation);
        if (string.IsNullOrWhiteSpace(returnUrl))
        {
            return null;
        }

        var url = returnUrl.Trim();
        if (Uri.TryCreate(url, UriKind.Absolute, out var absolute) && !absolute.IsFile)
        {
            // On this site and below its base address: the path from the host's root, path base included
            var baseUri = navigation.BaseUri;
            var onThisSite = url.StartsWith(baseUri, StringComparison.OrdinalIgnoreCase) ||
                             string.Equals(url + "/", baseUri, StringComparison.OrdinalIgnoreCase);
            return onThisSite ? SitePath(absolute.PathAndQuery + absolute.Fragment) : null;
        }

        return SitePath(url);
    }

    /// <summary>
    /// The address of <paramref name="url"/> on this site, for <c>NavigateTo</c> and links (ZA-219, Roadmap D101): a
    /// path that starts with <c>/</c> is read from the site's root, below its base address, as a route template is,
    /// and given as the path from the host's root with the path base, as ASP.NET's <c>Url.Content("~/…")</c> gives
    /// it. Under a path base <c>/site</c>, <c>/admin/users</c> becomes <c>/site/admin/users</c>;
    /// <c>NavigateTo("/admin/users")</c> or <c>href="/admin/users"</c> would leave the site. For the <see cref="Urls"/>
    /// constants, an <c>ISection.IndexUrl</c>, a tree item's address. No scheme or host, so a link is right behind a
    /// proxy too. An absolute address, a relative one (a media item's <c>Url</c>, resolved against the base address
    /// already) and one a browser reads as another host (<c>//host</c>) are returned as they are.
    /// </summary>
    /// <param name="navigation">The circuit's or page's navigation manager.</param>
    /// <param name="url">A path on this site, or any other address.</param>
    public static string ToSiteUrl(this NavigationManager navigation, string url)
    {
        ArgumentNullException.ThrowIfNull(navigation);
        ArgumentNullException.ThrowIfNull(url);
        return FromSiteRoot(url) ? new Uri(navigation.BaseUri).AbsolutePath + url[1..] : url;
    }

    /// <summary>
    /// The address to come back to, in the form a return address takes (<c>returnUrl</c>, the sign-in refresh's
    /// <c>redirectUrl</c>): a path from the host's root with the site's path base and the query string, as ASP.NET's
    /// cookie handler writes <c>ReturnUrl</c> and as <see cref="ToLocalReturnUrl"/> gives (ZA-219). The current
    /// page, or <paramref name="url"/> read as <see cref="ToSiteUrl"/> reads it.
    /// </summary>
    /// <param name="navigation">The circuit's or page's navigation manager.</param>
    /// <param name="url">A path on this site (<c>/admin/users/editrole/…</c>); null for the current page.</param>
    public static string GetReturnUrl(this NavigationManager navigation, string? url = null)
    {
        ArgumentNullException.ThrowIfNull(navigation);
        return navigation.ToAbsoluteUri(url == null ? navigation.Uri : navigation.ToSiteUrl(url)).PathAndQuery;
    }

    // A path from the site's or host's root, not one a browser reads as another host ("//host", "/\host")
    private static bool FromSiteRoot(string path) =>
        path.StartsWith('/') && !path.StartsWith("//", StringComparison.Ordinal) &&
        !path.StartsWith("/\\", StringComparison.Ordinal);

    // A return address: a path from the host's root, also not one a browser reads as another host once it drops the
    // tabs and line breaks in an address ("/\t/host", ZA-253), which ASP.NET's IsLocalUrl refuses too
    private static string? SitePath(string path) =>
        FromSiteRoot(path) && !path.Any(char.IsControl)
            ? path
            : null;

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