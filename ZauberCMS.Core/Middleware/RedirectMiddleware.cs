using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
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
/// </summary>
public class RedirectMiddleware(RequestDelegate next, ILogger<RedirectMiddleware> logger)
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

        // Exact matches first (letter case ignored)
        if (compiledRedirects.DirectMatches.TryGetValue(currentUrl, out var directRedirect) &&
            !RedirectRules.SameAddress(directRedirect.ToUrl, currentUrl))
        {
            PerformRedirect(context, directRedirect.ToUrl, directRedirect.IsPermanent);
            return true;
        }

        foreach (var regexRedirect in compiledRedirects.RegexMatches)
        {
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
    private async Task<CompiledRedirects> GetOrCreateCompiledRedirectsAsync(ISeoService seoService,
        ICacheService cacheService, CancellationToken cancellationToken)
    {
        return await cacheService.GetSetCachedItemAsync(CacheKey, async () =>
        {
            var allRedirects = await seoService.QueryRedirectsAsync(new QueryRedirectsParameters { Cached = false },
                cancellationToken);

            var directMatches = new Dictionary<string, RedirectInfo>(StringComparer.OrdinalIgnoreCase);
            var regexMatches = new List<RegexRedirectInfo>();

            foreach (var redirect in allRedirects)
            {
                // Rows saved before targets were validated are skipped rather than followed.
                if (string.IsNullOrWhiteSpace(redirect.FromUrl) || !RedirectTarget.IsValid(redirect.ToUrl))
                    continue;

                var redirectInfo = new RedirectInfo(redirect.ToUrl, redirect.IsPermanent);
                if (RedirectRules.IsRegex(redirect))
                {
                    try
                    {
                        regexMatches.Add(new RegexRedirectInfo(RedirectRules.CreateRegex(redirect.FromUrl, true),
                            redirect.ToUrl, redirect.IsPermanent));
                    }
                    catch (ArgumentException ex)
                    {
                        logger.LogError(ex, "Invalid regex pattern {Pattern}, treating as direct match", redirect.FromUrl);
                        directMatches.TryAdd(redirect.FromUrl, redirectInfo);
                    }
                }
                else
                {
                    // The newest of two redirects from the same address wins (the list is newest first)
                    directMatches.TryAdd(redirect.FromUrl, redirectInfo);
                }
            }

            logger.LogInformation("Compiled {DirectCount} direct redirects and {RegexCount} regex redirects",
                directMatches.Count, regexMatches.Count);

            return new CompiledRedirects(directMatches, regexMatches);
        }) ?? new CompiledRedirects(new Dictionary<string, RedirectInfo>(), []);
    }

    private void PerformRedirect(HttpContext context, string toUrl, bool isPermanent)
    {
        context.Response.StatusCode = isPermanent ? StatusCodes.Status301MovedPermanently : StatusCodes.Status302Found;
        context.Response.Headers.Location = toUrl;
        logger.LogInformation("Redirecting {From} -> {To} ({StatusCode})",
            context.Request.Path, toUrl, context.Response.StatusCode);
    }

    private sealed record CompiledRedirects(
        Dictionary<string, RedirectInfo> DirectMatches,
        List<RegexRedirectInfo> RegexMatches);

    private sealed record RedirectInfo(string ToUrl, bool IsPermanent);

    private sealed record RegexRedirectInfo(Regex Pattern, string ToUrl, bool IsPermanent);
}
