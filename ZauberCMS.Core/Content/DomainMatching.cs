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
    /// matches <c>/en</c> and the addresses below it, on whole segments (not <c>/english</c>, ZA-166). Letter case is
    /// ignored; the port is not compared.
    /// </summary>
    public static bool Matches(string? domainUrl, string host, string? path)
    {
        if (string.IsNullOrWhiteSpace(domainUrl))
        {
            return false;
        }

        var parts = domainUrl.Trim().Split('/', 2);
        if (!string.Equals(parts[0], host, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var domainPath = parts.Length > 1 ? parts[1].Trim('/') : string.Empty;
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
    /// order the domains were saved in (ZA-166).
    /// </summary>
    public static Domain? FindDomain(IEnumerable<Domain> domains, string host, string? path) =>
        domains.Where(d => Matches(d.Url, host, path))
            .OrderByDescending(d => d.Url!.Trim().TrimEnd('/').Length)
            .FirstOrDefault();
}
