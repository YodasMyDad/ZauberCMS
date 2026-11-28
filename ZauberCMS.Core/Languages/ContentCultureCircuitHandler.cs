using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Server.Circuits;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ZauberCMS.Core.Content.Interfaces;
using ZauberCMS.Core.Content.Parameters;
using ZauberCMS.Core.Extensions;
using ZauberCMS.Core.Membership.Middleware;
using ZauberCMS.Core.Settings;

namespace ZauberCMS.Core.Languages;

/// <summary>
/// Runs a front-end circuit (an interactive component on a site page) in the language of the page it is on, as
/// the page itself was rendered. The circuit's own request (<c>/_blazor</c>) is not a page, so without this it ran
/// in the default culture; before ZA-143 it ran in the language of the root page, whatever page it was on.
/// The language is looked up once per address the circuit is on (the lookup itself is cached).
/// </summary>
internal sealed class ContentCultureCircuitHandler(
    NavigationManager navigation,
    IContentService contentService,
    IOptions<ZauberSettings> settings,
    ILogger<ContentCultureCircuitHandler> logger) : CircuitHandler
{
    private string? _uri;
    private CultureInfo? _culture;

    /// <inheritdoc />
    public override Func<CircuitInboundActivityContext, Task> CreateInboundActivityHandler(
        Func<CircuitInboundActivityContext, Task> next)
    {
        return async context =>
        {
            var culture = await PageCultureAsync();
            if (culture != null)
            {
                // Set here, before the activity runs inside this method, so the activity and what it starts see it
                CultureInfo.CurrentCulture = culture;
                CultureInfo.CurrentUICulture = culture;
            }

            await next(context);
        };
    }

    private async Task<CultureInfo?> PageCultureAsync()
    {
        string uri;
        try
        {
            uri = navigation.Uri;
        }
        catch (InvalidOperationException)
        {
            return null; // the circuit has not been given its address yet
        }

        if (AdminAccess.IsAdminPath(navigation)) // below the path base (ZA-199)
        {
            return null; // AdminCultureCircuitHandler
        }

        if (uri == _uri)
        {
            return _culture;
        }

        CultureInfo? culture = null;
        try
        {
            // Relative to the site's base address, so a site under a path base (/site/about) finds "about"
            var relative = navigation.ToBaseRelativePath(uri);
            var end = relative.IndexOfAny(['?', '#']);
            var slug = Uri.UnescapeDataString(end < 0 ? relative : relative[..end]).Trim('/');
            var entry = await contentService.GetContentFromRequestAsync(new GetContentFromRequestParameters
            {
                Slug = slug.Length == 0 ? null : slug,
                IsRootContent = slug.Length == 0,
                // Without the path base, as the page request's middlewares match domains (ZA-190)
                Url = navigation.GetContentRequestUrl(uri)
            });
            if (entry.Content != null)
            {
                // Same rule as CultureMiddleware for the page request
                culture = CultureInfo.GetCultureInfo(entry.LanguageIsoCode ?? settings.Value.AdminDefaultLanguage);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Not remembered: the next activity tries again
            logger.LogWarning(ex, "Could not find the language of {Uri} for an interactive component", uri);
            return null;
        }

        _uri = uri;
        _culture = culture;
        return culture;
    }
}
