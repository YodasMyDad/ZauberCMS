using System.Globalization;
using ZauberCMS.Core.Content.Models;

namespace ZauberCMS.Core.Content;

/// <summary>
/// Whether a request is on one of the site's domains (<see cref="Domain.Url"/>): the rule content routing and
/// domain redirects share (ZA-165, Roadmap D42).
/// </summary>
internal static class DomainMatching
{
    /// <summary>
    /// True when a request to <paramref name="host"/> and <paramref name="path"/> is on the domain
    /// <paramref name="domainUrl"/>: <c>example.com</c> matches every address on that host, <c>example.com/en</c>
    /// matches <c>/en</c> and the addresses below it, on whole segments (not <c>/english</c>, ZA-166). Letter case,
    /// a scheme (<c>https://</c>), a trailing dot on the host and slashes around the path are ignored on both sides
    /// (<see cref="Normalize"/>, ZA-170). A domain with a port (<c>example.com:8080</c>) matches only a request to
    /// that port (when <paramref name="port"/> is given); one without matches any port.
    /// </summary>
    public static bool Matches(string? domainUrl, string host, string? path, int? port = null)
    {
        var domain = Normalize(domainUrl);
        var requestHost = NormalizeHost(host);
        if (domain == null || requestHost.Length == 0 ||
            !string.Equals(domain.Value.Host, requestHost, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (domain.Value.Port != null && port != null && domain.Value.Port != port)
        {
            return false;
        }

        var domainPath = domain.Value.Path;
        if (domainPath.Length == 0)
        {
            return true;
        }

        var requestPath = (path ?? string.Empty).TrimStart('/');
        return requestPath.StartsWith(domainPath, StringComparison.OrdinalIgnoreCase) &&
               (requestPath.Length == domainPath.Length || requestPath[domainPath.Length] == '/');
    }

    /// <summary>
    /// The domain a request to <paramref name="host"/> and <paramref name="path"/> belongs to: of those that match,
    /// the one with the longest path, so <c>example.com/fr</c> wins over <c>example.com</c> for <c>/fr/…</c> whatever
    /// order the domains were saved in (ZA-166); for the same path, one with a port before one without.
    /// </summary>
    public static Domain? FindDomain(IEnumerable<Domain> domains, string host, string? path, int? port = null) =>
        domains.Where(d => Matches(d.Url, host, path, port))
            .OrderByDescending(d => Normalize(d.Url)!.Value.Path.Length)
            .ThenByDescending(d => Normalize(d.Url)!.Value.Port != null)
            .FirstOrDefault();

    /// <summary>
    /// A domain address as it is matched: lower-case host without a scheme, port or trailing dot, the port if one is
    /// given, and the path without slashes around it. <c>HTTPS://Example.com.:8080/fr/</c> is host
    /// <c>example.com</c>, port 8080, path <c>fr</c>. Null when there is no host. Stored rows are not changed: this
    /// runs when they are matched, so a domain saved in any of these forms works (ZA-170).
    /// </summary>
    public static DomainAddress? Normalize(string? domainUrl)
    {
        if (string.IsNullOrWhiteSpace(domainUrl))
        {
            return null;
        }

        var value = domainUrl.Trim();
        var scheme = value.IndexOf("://", StringComparison.Ordinal);
        if (scheme >= 0)
        {
            value = value[(scheme + 3)..];
        }

        // The query string or fragment of a pasted address is not part of the domain
        var end = value.IndexOfAny(['?', '#']);
        if (end >= 0)
        {
            value = value[..end];
        }

        var slash = value.IndexOf('/');
        var authority = slash < 0 ? value : value[..slash];
        var path = slash < 0 ? string.Empty : value[(slash + 1)..].Trim('/');

        int? port = null;
        // The last colon starts the port, except inside an IPv6 address ([::1]:8080)
        var colon = authority.LastIndexOf(':');
        if (colon >= 0 && colon > authority.LastIndexOf(']'))
        {
            if (int.TryParse(authority[(colon + 1)..], NumberStyles.None, CultureInfo.InvariantCulture,
                    out var parsed))
            {
                port = parsed;
            }

            authority = authority[..colon];
        }

        var host = NormalizeHost(authority);
        return host.Length == 0 ? null : new DomainAddress(host, port, path);
    }

    /// <summary>A host as it is compared: lower case, without a trailing dot (<c>Example.com.</c> is <c>example.com</c>).</summary>
    private static string NormalizeHost(string? host) =>
        (host ?? string.Empty).Trim().TrimEnd('.').ToLowerInvariant();

    /// <summary>A normalised domain address (<see cref="Normalize"/>).</summary>
    internal readonly record struct DomainAddress(string Host, int? Port, string Path);
}
