using ZauberCMS.Core.Extensions;

namespace ZauberCMS.Core.Content;

/// <summary>
/// One set of rules for content URLs, used by the front-end lookup, the admin link editor and the services.
/// A content URL is stored lower case without leading or trailing slashes (<c>blog/my-post</c>); a link to it on
/// the site is that value with a leading slash (<c>/blog/my-post</c>).
/// </summary>
public static class ContentUrl
{
    /// <summary>
    /// A URL or request slug in the stored form: trimmed, backslashes made slashes, no leading or trailing
    /// slashes, lower case (invariant). Returns an empty string for null, blank or <c>/</c>. A full http/https
    /// address is only trimmed.
    /// </summary>
    public static string Normalize(string? url)
    {
        var value = url?.Trim() ?? string.Empty;
        if (IsHttpAddress(value))
        {
            return value;
        }

        return value.Replace('\\', '/').Trim('/').ToLowerInvariant();
    }

    /// <summary>
    /// The link to <paramref name="content"/> on this site (<c>/blog</c>), or null when it has no URL.
    /// </summary>
    public static string? SiteRelative(Models.Content content) => SiteRelative(content.Url());

    /// <summary>
    /// A stored content URL as a link on this site (<c>blog</c> becomes <c>/blog</c>), or null when empty.
    /// A full http/https address is returned as it is; anything else (also <c>javascript:…</c>) becomes a path on
    /// this site, so the result is never a script address or another host.
    /// </summary>
    public static string? SiteRelative(string? url)
    {
        var value = url?.Trim();
        if (string.IsNullOrEmpty(value))
        {
            return null;
        }

        if (IsHttpAddress(value))
        {
            return value;
        }

        var path = value.Replace('\\', '/').Trim('/');
        return path.Length == 0 ? "/" : "/" + path;
    }

    private static bool IsHttpAddress(string value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
        (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

    /// <summary>
    /// For a link that points at content (<c>data-contentid</c>): the href it should have now that the content's
    /// URL is <paramref name="contentUrl"/>, or null when <paramref name="currentHref"/> already points there.
    /// A query string or <c>#fragment</c> on the current href is kept.
    /// </summary>
    public static string? UpdatedHref(string? currentHref, string? contentUrl)
    {
        var target = SiteRelative(contentUrl);
        if (target == null)
        {
            return null;
        }

        var href = currentHref?.Trim() ?? string.Empty;
        var suffixStart = href.IndexOfAny(['?', '#']);
        var path = suffixStart >= 0 ? href[..suffixStart] : href;
        var suffix = suffixStart >= 0 ? href[suffixStart..] : string.Empty;

        if (path.Length > 0 && string.Equals(Normalize(path), Normalize(contentUrl), StringComparison.Ordinal))
        {
            return null;
        }

        return target + suffix;
    }
}
