using System.Text.RegularExpressions;
using Microsoft.Extensions.Localization;
using ZauberCMS.Core.Content;
using ZauberCMS.Core.Content.Models;
using ZauberCMS.Core.Seo.Models;

namespace ZauberCMS.Core.Seo;

/// <summary>
/// How an SEO redirect's From URL is matched (ZA-116, Roadmap D34), and the loop check a save makes.
/// </summary>
public static class RedirectRules
{
    /// <summary>How long one regular expression may take on one address.</summary>
    public static readonly TimeSpan MatchTimeout = TimeSpan.FromMilliseconds(100);

    /// <summary>The most redirects one chain is followed through when looking for a loop.</summary>
    public const int MaxChainLength = 20;

    private static readonly char[] RegexCharacters = ['(', ')', '[', ']', '{', '}', '|', '^', '$', '*', '+', '?', '\\'];

    /// <summary>
    /// The rule redirects followed before they had <see cref="SeoRedirect.IsRegex"/>: a From URL with any of
    /// <c>( ) [ ] { } | ^ $ * + ? \</c> is a regular expression. It still applies to redirects saved before then
    /// (<see cref="SeoRedirect.IsRegex"/> null) until they are saved again.
    /// </summary>
    public static bool LooksLikeRegex(string? fromUrl) => fromUrl?.IndexOfAny(RegexCharacters) >= 0;

    /// <summary>True when the redirect's From URL is matched as a regular expression.</summary>
    public static bool IsRegex(SeoRedirect redirect) => redirect.IsRegex ?? LooksLikeRegex(redirect.FromUrl);

    /// <summary>The From URL as a regular expression (ignoring case, with <see cref="MatchTimeout"/>).</summary>
    /// <exception cref="ArgumentException">The From URL is not a valid regular expression.</exception>
    public static Regex CreateRegex(string fromUrl, bool compiled = false) =>
        new(fromUrl, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | (compiled ? RegexOptions.Compiled : 0),
            MatchTimeout);

    /// <summary>
    /// True when two addresses (path and query) are the same as far as matching goes: letter case is ignored, as
    /// the redirect middleware ignores it.
    /// </summary>
    public static bool SameAddress(string? a, string? b) =>
        a != null && b != null && string.Equals(a.Trim(), b.Trim(), StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Why saving <paramref name="redirect"/> would make visitors go round in circles, or null when it would not:
    /// it sends an address to itself, or the redirects its target leads to (<paramref name="others"/>, the saved
    /// ones) come back to its From URL. Targets on another site end a chain.
    /// Without the site's domains, each domain a redirect names is taken to be a whole host of its own; use
    /// <see cref="FindLoop(SeoRedirect, IEnumerable{SeoRedirect}, IEnumerable{Domain}, IStringLocalizer)"/> where domains share a host
    /// (<c>example.com</c> and <c>example.com/fr</c>).
    /// </summary>
    /// <param name="text">ZauberCMS's text (<c>IStringLocalizer&lt;ZauberText&gt;</c>): the reason is in its language.</param>
    public static string? FindLoop(SeoRedirect redirect, IEnumerable<SeoRedirect> others, IStringLocalizer text) =>
        FindLoop(redirect, others, [], text);

    /// <summary>
    /// Why saving <paramref name="redirect"/> would make visitors go round in circles, or null when it would not:
    /// it sends an address to itself, or the redirects its target leads to (<paramref name="others"/>, the saved
    /// ones) come back to its From URL. Targets on another site end a chain.
    /// The chain is followed as requests follow it (ZA-169, Roadmap D42): on each host the redirect can fire on, at
    /// every step the address's domain is worked out from <paramref name="domains"/> by the rule content routing
    /// uses (the longest that matches), and only the redirects for that domain and the ones for every domain apply.
    /// </summary>
    /// <param name="text">ZauberCMS's text (<c>IStringLocalizer&lt;ZauberText&gt;</c>): the reason is in its language.</param>
    public static string? FindLoop(SeoRedirect redirect, IEnumerable<SeoRedirect> others, IEnumerable<Domain> domains,
        IStringLocalizer text)
    {
        if (!IsRegex(redirect) && SameAddress(redirect.FromUrl, redirect.ToUrl))
        {
            return text["seo_redirectToItself", redirect.FromUrl ?? string.Empty];
        }

        // In the order the middleware tries them: exact rules before regular expressions, within each the ones for a
        // domain before the ones for all, and then the newest first (the one being saved is the newest), then by id.
        var rules = others
            .Where(r => r.Id != redirect.Id && !string.IsNullOrWhiteSpace(r.FromUrl))
            .OrderByDescending(r => r.DateUpdated).ThenBy(r => r.Id)
            .Prepend(redirect)
            .Select(r => new Rule(r, IsRegex(r) ? TryCreateRegex(r.FromUrl!) : null, IsRegex(r)))
            .Where(r => !r.IsRegex || r.Regex != null)
            .OrderBy(r => r.IsRegex).ThenBy(r => r.Redirect.DomainId == null)
            .ToList();

        // A domain a redirect names that is not in the list (or no list was given) is a whole host of its own
        var siteDomains = domains.ToList();
        var known = siteDomains.Select(d => d.Id).ToHashSet();
        foreach (var id in rules.Select(r => r.Redirect.DomainId).OfType<Guid>().Distinct())
        {
            if (known.Add(id))
            {
                siteDomains.Add(new Domain { Id = id, Url = $"{id:N}.domain.invalid" });
            }
        }

        // With no redirect for a domain, every host follows the same chain: once is enough
        var hosts = rules.Any(r => r.Redirect.DomainId != null)
            ? HostsToFollow(redirect, siteDomains)
            : [(string.Empty, 0)];
        foreach (var (host, port) in hosts)
        {
            var loop = FollowChain(redirect, rules, siteDomains, host, port, text);
            if (loop != null)
            {
                return loop;
            }
        }

        return null;
    }

    /// <summary>
    /// The hosts (and ports) a request can come in on for the chain to start with <paramref name="redirect"/>: its
    /// domain's, or for one without a domain every domain's host and a host that is on no domain. Port 0 stands for
    /// a port no domain names.
    /// </summary>
    private static List<(string Host, int Port)> HostsToFollow(SeoRedirect redirect, List<Domain> domains)
    {
        var addresses = domains
            .Where(d => redirect.DomainId == null || d.Id == redirect.DomainId)
            .Select(d => DomainMatching.Normalize(d.Url))
            .OfType<DomainMatching.DomainAddress>()
            .ToList();

        var hosts = addresses.Select(a => (a.Host, Port: a.Port ?? 0)).Distinct().ToList();
        if (redirect.DomainId == null)
        {
            // The same hosts on a port no domain names, and a host with no domain at all
            hosts = hosts.Concat(addresses.Select(a => (a.Host, Port: 0)))
                .Append((string.Empty, 0))
                .Distinct().ToList();
        }

        return hosts;
    }

    /// <summary>Follows the chain from <paramref name="redirect"/>'s target as a request to <paramref name="host"/> would.</summary>
    private static string? FollowChain(SeoRedirect redirect, List<Rule> rules, List<Domain> domains, string host,
        int port, IStringLocalizer text)
    {
        var current = redirect.ToUrl;
        for (var step = 0; step < MaxChainLength; step++)
        {
            if (current == null || !current.StartsWith('/'))
            {
                return null;
            }

            // The address's domain, as the middleware works it out for a request to it (on no host: none)
            var address = current;
            var addressDomain = host.Length == 0 ? null : DomainMatching.FindDomain(domains, host, PathOf(address), port)?.Id;

            // As the middleware does: a rule never sends an address to that same address
            var next = rules.FirstOrDefault(r =>
                (r.Redirect.DomainId == null || r.Redirect.DomainId == addressDomain) &&
                r.Matches(address) && !SameAddress(r.Redirect.ToUrl, address));
            if (next == null)
            {
                return null;
            }

            if (next.Redirect == redirect)
            {
                return text["seo_redirectLoop", redirect.ToUrl ?? string.Empty, redirect.FromUrl ?? string.Empty];
            }

            current = next.Redirect.ToUrl;
        }

        return text["seo_redirectChainTooLong", redirect.ToUrl ?? string.Empty, MaxChainLength];
    }

    /// <summary>The path of an address (path and query), which is what domains match on.</summary>
    private static string PathOf(string address)
    {
        var end = address.IndexOfAny(['?', '#']);
        return end < 0 ? address : address[..end];
    }

    private static Regex? TryCreateRegex(string pattern)
    {
        try
        {
            return CreateRegex(pattern);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    private sealed record Rule(SeoRedirect Redirect, Regex? Regex, bool IsRegex)
    {
        public bool Matches(string address)
        {
            if (!IsRegex)
            {
                return SameAddress(Redirect.FromUrl, address);
            }

            try
            {
                return Regex!.IsMatch(address);
            }
            catch (RegexMatchTimeoutException)
            {
                return false;
            }
        }
    }
}
