using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ZauberCMS.Core.Content;
using ZauberCMS.Core.Content.Interfaces;
using ZauberCMS.Core.Content.Parameters;
using ZauberCMS.Core.Seo;
using ZauberCMS.Core.Seo.Interfaces;
using ZauberCMS.Core.Seo.Models;
using ZauberCMS.Core.Seo.Parameters;
using ZauberCMS.Core.Shared.Services;

namespace ZauberCMS.Core.Middleware;

/// <summary>
/// Sends visitors on from the SEO redirects saved in the admin. An exact From URL is looked up directly; one marked
/// as a regular expression (<see cref="SeoRedirect.IsRegex"/>) is tried in turn after that (ZA-116). The compiled
/// list is kept in <see cref="ICacheService"/> under the <c>SeoRedirect</c> prefix, so saving or deleting a redirect
/// clears it on every server (ZA-115).
/// A redirect with a domain (<see cref="SeoRedirect.DomainId"/>) applies only to requests on that domain, matched by
/// the rule content routing uses; one without applies on every host. For the same address, a redirect for the
/// request's domain wins over one for all domains (ZA-165, Roadmap D42).
/// </summary>
internal class RedirectMiddleware(RequestDelegate next, ILogger<RedirectMiddleware> logger)
{
    /// <summary>The cache key of the compiled list: starts with the prefix a redirect save clears.</summary>
    internal const string CacheKey = nameof(SeoRedirect) + "-CompiledRedirects";

    public async Task InvokeAsync(HttpContext context, ISeoService seoService, ICacheService cacheService)
    {
        // Only finding the redirect is inside the try: the rest of the request runs once, after it. A page that
        // threw used to be caught here, logged as this middleware's error and run a second time (ZA-098).
        try
        {
            if (await TryRedirectAsync(context, seoService, cacheService))
            {
                return;
            }
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            // The visitor went away (ZA-111)
            return;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in RedirectMiddleware");
        }

        await next(context);
    }

    /// <summary>Sends the redirect that matches the request, if any; true when one was sent.</summary>
    private async Task<bool> TryRedirectAsync(HttpContext context, ISeoService seoService, ICacheService cacheService)
    {
        // Skip admin routes, API routes, account routes, static files, and the error page of a failed request (a broad
        // pattern must not turn a 500 into a redirect)
        if (context.Features.Get<IExceptionHandlerFeature>() != null ||
            SitePaths.IsSystemPath(context.Request.Path) ||
            (context.Request.Path.Value?.Contains('.') ?? false))
        {
            return false;
        }

        var compiledRedirects = await GetOrCreateCompiledRedirectsAsync(seoService, cacheService,
            context.RequestAborted);
        if (compiledRedirects.DirectMatches.Count == 0 && compiledRedirects.RegexMatches.Count == 0)
        {
            return false;
        }

        var currentUrl = $"{context.Request.Path}{context.Request.QueryString}";

        // The domains this request is on, read from the cached domain list only when a redirect has a domain. It is
        // read on each request, so an edited domain is matched by its new address.
        var requestDomains = compiledRedirects.HasDomainRedirects
            ? await GetRequestDomainsAsync(context)
            : [];

        // Exact matches first (letter case ignored); each address lists the redirects for a domain before the ones
        // for all domains
        if (compiledRedirects.DirectMatches.TryGetValue(currentUrl, out var directRedirects) &&
            directRedirects.FirstOrDefault(r => AppliesTo(r.DomainId, requestDomains) &&
                                                !RedirectRules.SameAddress(r.ToUrl, currentUrl)) is { } directRedirect)
        {
            PerformRedirect(context, directRedirect.ToUrl, directRedirect.IsPermanent);
            return true;
        }

        foreach (var regexRedirect in compiledRedirects.RegexMatches)
        {
            if (!AppliesTo(regexRedirect.DomainId, requestDomains))
            {
                continue;
            }

            try
            {
                // A pattern that also matches its own target would send the visitor to the same address again
                if (regexRedirect.Pattern.IsMatch(currentUrl) &&
                    !RedirectRules.SameAddress(regexRedirect.ToUrl, currentUrl))
                {
                    PerformRedirect(context, regexRedirect.ToUrl, regexRedirect.IsPermanent);
                    return true;
                }
            }
            catch (RegexMatchTimeoutException)
            {
                logger.LogWarning("Regex timeout for pattern {Pattern}", regexRedirect.Pattern);
            }
        }

        return false;
    }

    /// <summary>True when a redirect for <paramref name="domainId"/> (null: every domain) applies to the request.</summary>
    private static bool AppliesTo(Guid? domainId, HashSet<Guid> requestDomains) =>
        domainId == null || requestDomains.Contains(domainId.Value);

    /// <summary>The ids of the site's domains the request is on (<see cref="DomainMatching.Matches"/>).</summary>
    private static async Task<HashSet<Guid>> GetRequestDomainsAsync(HttpContext context)
    {
        var contentService = context.RequestServices.GetRequiredService<IContentService>();
        var domains = await contentService.GetCachedDomainsAsync(new CachedDomainsParameters(),
            context.RequestAborted);
        return domains
            .Where(d => DomainMatching.Matches(d.Url, context.Request.Host.Host, context.Request.Path.Value))
            .Select(d => d.Id)
            .ToHashSet();
    }

    private async Task<CompiledRedirects> GetOrCreateCompiledRedirectsAsync(ISeoService seoService,
        ICacheService cacheService, CancellationToken cancellationToken)
    {
        return await cacheService.GetSetCachedItemAsync(CacheKey, async () =>
        {
            var allRedirects = await seoService.QueryRedirectsAsync(new QueryRedirectsParameters { Cached = false },
                cancellationToken);

            var directMatches = new Dictionary<string, List<RedirectInfo>>(StringComparer.OrdinalIgnoreCase);
            var regexMatches = new List<RegexRedirectInfo>();

            // The redirects for a domain before the ones for every domain, each newest first (the list is newest
            // first and the sort keeps that order): where both apply, the one for the request's domain wins
            foreach (var redirect in allRedirects.OrderBy(r => r.DomainId == null))
            {
                // Rows saved before targets were validated are skipped rather than followed.
                if (string.IsNullOrWhiteSpace(redirect.FromUrl) || !RedirectTarget.IsValid(redirect.ToUrl))
                    continue;

                var redirectInfo = new RedirectInfo(redirect.ToUrl, redirect.IsPermanent, redirect.DomainId);
                if (RedirectRules.IsRegex(redirect))
                {
                    try
                    {
                        regexMatches.Add(new RegexRedirectInfo(RedirectRules.CreateRegex(redirect.FromUrl, true),
                            redirect.ToUrl, redirect.IsPermanent, redirect.DomainId));
                    }
                    catch (ArgumentException ex)
                    {
                        logger.LogError(ex, "Invalid regex pattern {Pattern}, treating as direct match", redirect.FromUrl);
                        AddDirect(directMatches, redirect.FromUrl, redirectInfo);
                    }
                }
                else
                {
                    AddDirect(directMatches, redirect.FromUrl, redirectInfo);
                }
            }

            logger.LogInformation("Compiled {DirectCount} direct redirects and {RegexCount} regex redirects",
                directMatches.Count, regexMatches.Count);

            return new CompiledRedirects(directMatches, regexMatches, allRedirects.Any(r => r.DomainId != null));
        }) ?? new CompiledRedirects(new Dictionary<string, List<RedirectInfo>>(), [], false);
    }

    /// <summary>Adds a redirect from <paramref name="fromUrl"/>, after the ones from that address added before it.</summary>
    private static void AddDirect(Dictionary<string, List<RedirectInfo>> directMatches, string fromUrl,
        RedirectInfo redirectInfo)
    {
        if (!directMatches.TryGetValue(fromUrl, out var redirects))
        {
            directMatches[fromUrl] = redirects = [];
        }

        redirects.Add(redirectInfo);
    }

    private void PerformRedirect(HttpContext context, string toUrl, bool isPermanent)
    {
        context.Response.StatusCode = isPermanent ? StatusCodes.Status301MovedPermanently : StatusCodes.Status302Found;
        context.Response.Headers.Location = toUrl;
        logger.LogInformation("Redirecting {From} -> {To} ({StatusCode})",
            context.Request.Path, toUrl, context.Response.StatusCode);
    }

    /// <param name="DirectMatches">By From URL: the redirects from that address, the ones for a domain first.</param>
    /// <param name="RegexMatches">In the order they are tried: the ones for a domain first.</param>
    /// <param name="HasDomainRedirects">True when a redirect has a domain, so the request's domains are needed.</param>
    private sealed record CompiledRedirects(
        Dictionary<string, List<RedirectInfo>> DirectMatches,
        List<RegexRedirectInfo> RegexMatches,
        bool HasDomainRedirects);

    private sealed record RedirectInfo(string ToUrl, bool IsPermanent, Guid? DomainId);

    private sealed record RegexRedirectInfo(Regex Pattern, string ToUrl, bool IsPermanent, Guid? DomainId);
}
