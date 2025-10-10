using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using ZauberCMS.Core.Seo.Interfaces;
using ZauberCMS.Core.Seo.Models;
using ZauberCMS.Core.Seo.Parameters;

namespace ZauberCMS.Core.Middleware;

public class RedirectMiddleware(
    RequestDelegate next,
    ILogger<RedirectMiddleware> logger,
    IMemoryCache memoryCache)
{
    private const string CacheKey = "RedirectMiddleware.CompiledRedirects";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);
    
    public async Task InvokeAsync(HttpContext context, ISeoService seoService)
    {
        try
        {
            var path = context.Request.Path.Value?.ToLower() ?? string.Empty;
            
            // Skip admin routes, API routes, account routes, and static files
            if (path.StartsWith("/admin") || 
                path.StartsWith("/api") || 
                path.StartsWith("/account") ||
                path.Contains("."))
            {
                await next(context);
                return;
            }
            
            var compiledRedirects = await GetOrCreateCompiledRedirectsAsync(seoService);
            
            // Early exit if no redirects configured
            if (compiledRedirects.DirectMatches.Count == 0 && compiledRedirects.RegexMatches.Count == 0)
            {
                await next(context);
                return;
            }
            
            var currentUrl = $"{context.Request.Path}{context.Request.QueryString}";
            
            // Check direct matches first (O(1) lookup)
            if (compiledRedirects.DirectMatches.TryGetValue(currentUrl, out var directRedirect))
            {
                PerformRedirect(context, directRedirect.ToUrl, directRedirect.IsPermanent);
                return;
            }
            
            // Case-insensitive direct match
            var currentUrlLower = currentUrl.ToLowerInvariant();
            if (compiledRedirects.DirectMatchesLowerCase.TryGetValue(currentUrlLower, out var caseInsensitiveRedirect))
            {
                PerformRedirect(context, caseInsensitiveRedirect.ToUrl, caseInsensitiveRedirect.IsPermanent);
                return;
            }
            
            // Check regex matches only if no direct match found
            foreach (var regexRedirect in compiledRedirects.RegexMatches)
            {
                try
                {
                    if (regexRedirect.Pattern.IsMatch(currentUrl))
                    {
                        PerformRedirect(context, regexRedirect.ToUrl, regexRedirect.IsPermanent);
                        return;
                    }
                }
                catch (RegexMatchTimeoutException)
                {
                    logger.LogWarning("Regex timeout for pattern {Pattern}", regexRedirect.Pattern);
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in RedirectMiddleware");
        }
        
        await next(context);
    }
    
    private async Task<CompiledRedirects> GetOrCreateCompiledRedirectsAsync(ISeoService seoService)
    {
        return await memoryCache.GetOrCreateAsync(CacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheDuration;
            
            var allRedirects = await seoService.QueryRedirectsAsync(new QueryRedirectsParameters { Cached = true });
            
            var directMatches = new Dictionary<string, RedirectInfo>(StringComparer.Ordinal);
            var directMatchesLowerCase = new Dictionary<string, RedirectInfo>(StringComparer.OrdinalIgnoreCase);
            var regexMatches = new List<RegexRedirectInfo>();
            
            foreach (var redirect in allRedirects)
            {
                if (string.IsNullOrWhiteSpace(redirect.FromUrl) || string.IsNullOrWhiteSpace(redirect.ToUrl))
                    continue;
                
                var redirectInfo = new RedirectInfo(redirect.ToUrl, redirect.IsPermanent);
                
                // Try to determine if it's a regex pattern
                if (IsLikelyRegexPattern(redirect.FromUrl))
                {
                    try
                    {
                        var regex = new Regex(
                            redirect.FromUrl, 
                            RegexOptions.IgnoreCase | RegexOptions.Compiled, 
                            TimeSpan.FromMilliseconds(100));
                        
                        regexMatches.Add(new RegexRedirectInfo(regex, redirect.ToUrl, redirect.IsPermanent));
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Invalid regex pattern {Pattern}, treating as direct match", redirect.FromUrl);
                        // Fallback to direct match if regex compilation fails
                        directMatches[redirect.FromUrl] = redirectInfo;
                        directMatchesLowerCase[redirect.FromUrl.ToLowerInvariant()] = redirectInfo;
                    }
                }
                else
                {
                    // Store both case-sensitive and case-insensitive for performance
                    directMatches[redirect.FromUrl] = redirectInfo;
                    directMatchesLowerCase[redirect.FromUrl.ToLowerInvariant()] = redirectInfo;
                }
            }
            
            logger.LogInformation("Compiled {DirectCount} direct redirects and {RegexCount} regex redirects", 
                directMatches.Count, regexMatches.Count);
            
            return new CompiledRedirects(directMatches, directMatchesLowerCase, regexMatches);
        }) ?? new CompiledRedirects([], [], []);
    }
    
    private static bool IsLikelyRegexPattern(string pattern)
    {
        // Heuristic: if it contains regex metacharacters, treat it as regex
        return pattern.IndexOfAny(['(', ')', '[', ']', '{', '}', '|', '^', '$', '*', '+', '?', '\\']) >= 0;
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
        Dictionary<string, RedirectInfo> DirectMatchesLowerCase,
        List<RegexRedirectInfo> RegexMatches);
    
    private sealed record RedirectInfo(string ToUrl, bool IsPermanent);
    
    private sealed record RegexRedirectInfo(Regex Pattern, string ToUrl, bool IsPermanent);
}

