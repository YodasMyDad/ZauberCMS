using System.Globalization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ZauberCMS.Core.Content.Interfaces;
using ZauberCMS.Core.Content.Models;
using ZauberCMS.Core.Content.Parameters;
using ZauberCMS.Core.Languages;
using ZauberCMS.Core.Membership.Middleware;
using ZauberCMS.Core.Settings;

namespace ZauberCMS.Core.Middleware;

public class CultureMiddleware(
    RequestDelegate next,
    IOptions<ZauberSettings> settings,
    ILogger<CultureMiddleware> logger)
{
    private const string EntryModelKey = "ZauberCMS.EntryModel";
    
    public async Task InvokeAsync(HttpContext context, IContentService contentService)
    {
        try
        {
            // Only process if this looks like a CMS route (not admin, not static files, etc.)
            var path = context.Request.Path.Value?.ToLowerInvariant() ?? string.Empty;

            // The admin has its own language, whatever the browser asks for and whatever the content is in
            if (AdminAccess.IsAdminPath(context.Request.Path))
            {
                var adminCulture = AdminCulture.Apply(settings.Value);
                context.Features.Set<IRequestCultureFeature>(
                    new RequestCultureFeature(new RequestCulture(adminCulture, adminCulture), null));
            }
            
            // Only requests for the content catch-all page are content. Anything else (/_blazor, a site's own
            // endpoints, files) used to be looked up as the root page and got its language (ZA-143).
            if (path.StartsWith("/admin") ||
                path.StartsWith("/api") ||
                path.StartsWith("/account") ||
                path.Contains('.') ||
                !IsContentEndpoint(context.GetEndpoint()))
            {
                await next(context);
                return;
            }
            
            // Get the slug from route data
            var slug = context.Request.RouteValues["slug"]?.ToString();
            
            // Get content from database (this is cached for 5 minutes)
            var entryModel = await contentService.GetContentFromRequestAsync(new GetContentFromRequestParameters 
            { 
                Slug = slug, 
                IsRootContent = slug == null, 
                Url = $"{context.Request.Scheme}://{context.Request.Host}{context.Request.Path}{context.Request.QueryString}"
            });

            // Store in HttpContext.Items so EntryPage can reuse it without another query
            context.Items[EntryModelKey] = entryModel;

            if (entryModel.Content != null)
            {
                var languageIso = entryModel.LanguageIsoCode ?? settings.Value.AdminDefaultLanguage;
                var cultureInfo = new CultureInfo(languageIso);
                
                // Set the culture on the HttpContext using the proper localization feature
                var requestCulture = new RequestCulture(cultureInfo, cultureInfo);
                context.Features.Set<IRequestCultureFeature>(new RequestCultureFeature(requestCulture, null));
                
                // Also set thread culture for good measure
                CultureInfo.CurrentCulture = cultureInfo;
                CultureInfo.CurrentUICulture = cultureInfo;
                
                logger.LogDebug("Culture set to {Culture} for path {Path}", languageIso, path);
            }
            else
            {
                // No content found - will use default culture from UseRequestLocalization
                logger.LogDebug("No content found for path {Path}, using default culture", path);
            }
        }
        catch (Exception ex)
        {
            // If anything fails, continue with default culture
            // We don't want to break the request pipeline
            logger.LogError(ex, "Error in CultureMiddleware for path {Path}", context.Request.Path);
        }

        await next(context);
    }
    
    /// <summary>
    /// True for the front-end catch-all page (<c>@page "/{**slug}"</c> in ZauberCMS.Routing, or a site's own
    /// replacement with the same route): a route that is nothing but a catch-all <c>slug</c> parameter.
    /// </summary>
    public static bool IsContentEndpoint(Endpoint? endpoint) =>
        endpoint is RouteEndpoint { RoutePattern: var pattern } &&
        pattern.PathSegments.Count == 1 &&
        pattern.Parameters is [{ Name: "slug", IsCatchAll: true }];

    /// <summary>
    /// Helper method to retrieve the cached EntryModel from HttpContext.
    /// 
    /// Note: In Blazor SSR with enhanced navigation, each navigation is still a full server request,
    /// so this middleware runs and HttpContext.Items is populated for every page transition.
    /// If enhanced navigation is disabled or unavailable, EntryPage has a fallback query.
    /// </summary>
    public static EntryModel? GetEntryModel(HttpContext? context)
    {
        if (context?.Items.TryGetValue(EntryModelKey, out var entryModel) == true)
        {
            return entryModel as EntryModel;
        }
        return null;
    }
}

