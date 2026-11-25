namespace ZauberCMS.Core.Seo;

/// <summary>Rules for where an SEO redirect may send a visitor.</summary>
public static class RedirectTarget
{
    /// <summary>
    /// True for a path on this site (<c>/new-page</c>) or a full http/https address.
    /// Refused: empty values, other schemes (<c>javascript:</c>, <c>data:</c>), and addresses a browser reads as
    /// another site although they look like a path (<c>//host</c>, <c>/\host</c>).
    /// </summary>
    public static bool IsValid([System.Diagnostics.CodeAnalysis.NotNullWhen(true)] string? toUrl)
    {
        if (string.IsNullOrWhiteSpace(toUrl) || toUrl.Any(char.IsControl))
        {
            return false;
        }

        if (toUrl.StartsWith('/'))
        {
            return toUrl.Length == 1 || (toUrl[1] != '/' && toUrl[1] != '\\');
        }

        return Uri.TryCreate(toUrl, UriKind.Absolute, out var uri) &&
               (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
    }
}
