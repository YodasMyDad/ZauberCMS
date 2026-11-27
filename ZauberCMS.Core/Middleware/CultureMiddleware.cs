using System.Globalization;
using Microsoft.AspNetCore.Components.Endpoints;
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
        // The error page runs the pipeline again for the same request (ZA-098): the failed page's model must not
        // be shown in its place
        context.Items.Remove(EntryModelKey);

        // Only this middleware's own work is inside the try: the rest of the request runs once, after it. A page
        // that threw used to be caught here, logged as this middleware's error and run a second time (ZA-098).
        try
        {
            // Set here, not in the method: a culture set inside an async method does not reach its caller
            if (await ResolveCultureAsync(context, contentService) is { } culture)
            {
                CultureInfo.CurrentCulture = culture;
                CultureInfo.CurrentUICulture = culture;
            }
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            // The visitor went away (ZA-111): nobody is waiting for the page
            return;
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
    /// The admin's culture for the admin; for a content page, the page (kept for EntryPage) and its culture; null
    /// for anything else. Also sets the request's culture feature.
    /// </summary>
    private async Task<CultureInfo?> ResolveCultureAsync(HttpContext context, IContentService contentService)
    {
        CultureInfo? culture = null;
        // Only process if this looks like a CMS route (not admin, not static files, etc.)
        var path = context.Request.Path.Value?.ToLowerInvariant() ?? string.Empty;

        // The admin has its own language, whatever the browser asks for and whatever the content is in
        if (AdminAccess.IsAdminPath(context.Request.Path))
        {
            var adminCulture = AdminCulture.Get(settings.Value);
            context.Features.Set<IRequestCultureFeature>(
                new RequestCultureFeature(new RequestCulture(adminCulture, adminCulture), null));
            culture = adminCulture;
        }
        
        // Only requests for the home page and the content catch-all page are content. Anything else
        // (/_blazor, a site's own endpoints, files) used to be looked up as the root page and got its
        // language (ZA-143).
        if (SitePaths.IsSystemPath(context.Request.Path) ||
            path.Contains('.') ||
            !IsContentEndpoint(context.GetEndpoint()))
        {
            return culture;
        }
        
        // Get the slug from route data
        var slug = context.Request.RouteValues["slug"]?.ToString();
        
        // Get content from database (this is cached for 5 minutes)
        var entryModel = await contentService.GetContentFromRequestAsync(new GetContentFromRequestParameters 
        { 
            Slug = slug, 
            IsRootContent = slug == null, 
            Url = $"{context.Request.Scheme}://{context.Request.Host}{context.Request.Path}{context.Request.QueryString}"
        }, context.RequestAborted);

        // Store in HttpContext.Items so EntryPage can reuse it without another query
        context.Items[EntryModelKey] = entryModel;

        if (entryModel.Content != null)
        {
            var languageIso = entryModel.LanguageIsoCode ?? settings.Value.AdminDefaultLanguage;
            var cultureInfo = new CultureInfo(languageIso);
            
            // Set the culture on the HttpContext using the proper localization feature
            var requestCulture = new RequestCulture(cultureInfo, cultureInfo);
            context.Features.Set<IRequestCultureFeature>(new RequestCultureFeature(requestCulture, null));
            
            // And the thread culture, for the rest of the request (set by the caller)
            culture = cultureInfo;
            
            logger.LogDebug("Culture set to {Culture} for path {Path}", languageIso, path);
        }
        else
        {
            // No content found - will use default culture from UseRequestLocalization
            logger.LogDebug("No content found for path {Path}, using default culture", path);
        }

        return culture;
    }
    
    /// <summary>
    /// True for the front-end catch-all page (<c>@page "/{**slug}"</c> in ZauberCMS.Routing, or a site's own
    /// replacement with the same route): a route that is nothing but a catch-all <c>slug</c> parameter.
    /// </summary>
    public static bool IsContentEndpoint(Endpoint? endpoint)
    {
        if (endpoint is not RouteEndpoint { RoutePattern: var pattern })
        {
            return false;
        }

        // The home page. "/" has a page of its own (Index.razor: an exact route wins over the catch-all), so it
        // never arrives as the catch-all. Any Razor page at "/" is the site's home page; an endpoint of another
        // kind there (a site's own MapGet("/")) is not content (ZA-148).
        if (pattern.PathSegments.Count == 0)
        {
            return endpoint.Metadata.GetMetadata<ComponentTypeMetadata>() != null;
        }

        return pattern.PathSegments.Count == 1 &&
               pattern.Parameters is [{ Name: "slug", IsCatchAll: true }];
    }

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

