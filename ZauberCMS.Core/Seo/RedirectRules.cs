using System.Text.RegularExpressions;
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
    /// </summary>
    public static string? FindLoop(SeoRedirect redirect, IEnumerable<SeoRedirect> others)
    {
        // In the order the middleware tries them: exact rules before regular expressions, within each the ones for a
        // domain before the ones for all, and then the newest first (the one being saved is the newest), then by id.
        // Only rules that can apply on the same requests count: a redirect for another domain never follows this one
        // (ZA-165, D42).
        var rules = others
            .Where(r => r.Id != redirect.Id && !string.IsNullOrWhiteSpace(r.FromUrl))
            .Where(r => r.DomainId == null || redirect.DomainId == null || r.DomainId == redirect.DomainId)
            .OrderByDescending(r => r.DateUpdated).ThenBy(r => r.Id)
            .Prepend(redirect)
            .Select(r => new Rule(r, IsRegex(r) ? TryCreateRegex(r.FromUrl!) : null, IsRegex(r)))
            .Where(r => !r.IsRegex || r.Regex != null)
            .OrderBy(r => r.IsRegex).ThenBy(r => r.Redirect.DomainId == null)
            .ToList();

        if (!IsRegex(redirect) && SameAddress(redirect.FromUrl, redirect.ToUrl))
        {
            return $"{redirect.FromUrl} would redirect to itself";
        }

        var current = redirect.ToUrl;
        for (var step = 0; step < MaxChainLength; step++)
        {
            if (current == null || !current.StartsWith('/'))
            {
                return null;
            }

            // As the middleware does: a rule never sends an address to that same address
            var address = current;
            var next = rules.FirstOrDefault(r => r.Matches(address) && !SameAddress(r.Redirect.ToUrl, address));
            if (next == null)
            {
                return null;
            }

            if (next.Redirect == redirect)
            {
                return $"{redirect.ToUrl} leads back to {redirect.FromUrl} through other redirects, so visitors would go round in circles";
            }

            current = next.Redirect.ToUrl;
        }

        return $"{redirect.ToUrl} leads through more than {MaxChainLength} redirects";
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
