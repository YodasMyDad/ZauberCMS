using System.Text.RegularExpressions;
using AngleSharp.Dom;
using Ganss.Xss;
using Microsoft.Extensions.Options;
using ZauberCMS.Core.Settings;

namespace ZauberCMS.Core.Shared.Services;

/// <summary>
/// Default implementation of HTML sanitization using HtmlSanitizer library.
/// Configured with safe defaults for CMS content:
/// <list type="bullet">
/// <item>scripts, event handlers and unknown schemes are removed (library defaults);</item>
/// <item><c>&lt;iframe&gt;</c> is kept only when its <c>src</c> is an https address on an allowed host
/// (<c>Zauber:HtmlSanitizer:AllowedIframeHosts</c>, by default YouTube and Vimeo);</item>
/// <item><c>data:</c> addresses are kept only as the source of an <c>&lt;img&gt;</c> holding a bitmap;</item>
/// <item>video embed placeholders written by the rich text editor are turned into players by
/// <see cref="IRichTextEmbedService"/> after sanitising, so the result is ready to render.</item>
/// </list>
/// </summary>
public partial class DefaultHtmlSanitizerService : IHtmlSanitizerService
{
    /// <summary>Hosts an iframe may load from when the setting is not configured.</summary>
    public static readonly IReadOnlyList<string> DefaultIframeHosts =
        ["www.youtube.com", "www.youtube-nocookie.com", "player.vimeo.com"];

    private readonly HtmlSanitizer _sanitizer;
    private readonly HashSet<string> _iframeHosts;
    private readonly IRichTextEmbedService _embeds;

    public DefaultHtmlSanitizerService() : this(null)
    {
    }

    public DefaultHtmlSanitizerService(IOptions<ZauberSettings>? settings, IRichTextEmbedService? embeds = null)
    {
        _embeds = embeds ?? new DefaultRichTextEmbedService();
        _iframeHosts = new HashSet<string>(
            (settings?.Value.HtmlSanitizer.AllowedIframeHosts ?? DefaultIframeHosts)
            .Where(host => !string.IsNullOrWhiteSpace(host))
            .Select(host => host.Trim()),
            StringComparer.OrdinalIgnoreCase);

        _sanitizer = new HtmlSanitizer();

        // Allow common HTML elements and attributes needed for rich text content
        _sanitizer.AllowedTags.Add("iframe"); // For video embeds; the source is checked in RemoveForeignIframes
        _sanitizer.AllowedAttributes.Add("class");
        _sanitizer.AllowedAttributes.Add("style");
        _sanitizer.AllowedAttributes.Add("allow");
        _sanitizer.AllowedAttributes.Add("allowfullscreen");
        _sanitizer.AllowedAttributes.Add("frameborder");
        _sanitizer.AllowedAttributes.Add("loading");
        _sanitizer.AllowedAttributes.Add("referrerpolicy");

        // Allow data attributes for all tags
        _sanitizer.AllowDataAttributes = true;

        // Allow specific schemes. "data" is narrowed to bitmaps in <img> by KeepDataUrlsForImagesOnly.
        _sanitizer.AllowedSchemes.Add("data");
        _sanitizer.AllowedSchemes.Add("http");
        _sanitizer.AllowedSchemes.Add("https");

        _sanitizer.AllowedCssProperties.Add("width");
        _sanitizer.AllowedCssProperties.Add("height");
        _sanitizer.AllowedCssProperties.Add("border");

        // Use UriAttributes to control which attributes contain URLs
        _sanitizer.UriAttributes.Add("src");
        _sanitizer.UriAttributes.Add("href");

        _sanitizer.FilterUrl += KeepDataUrlsForImagesOnly;
        _sanitizer.PostProcessNode += RemoveForeignIframes;
    }

    public string Sanitize(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return string.Empty;
        }

        // Placeholders are expanded after sanitising: only they become iframes, and only for known providers.
        return _embeds.Expand(_sanitizer.Sanitize(html));
    }

    /// <summary>
    /// A data: address can hold a whole HTML document or an SVG with script. The only use rich text has for it is
    /// an inline bitmap, so everything else is dropped.
    /// </summary>
    private static void KeepDataUrlsForImagesOnly(object? sender, FilterUrlEventArgs e)
    {
        var url = e.SanitizedUrl?.TrimStart();
        if (url == null || !url.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var isImage = string.Equals(e.Tag.LocalName, "img", StringComparison.OrdinalIgnoreCase);
        if (!isImage || !BitmapDataUrl().IsMatch(url))
        {
            e.SanitizedUrl = null;
        }
    }

    /// <summary>An iframe shows another site inside the page; only the configured video hosts may be shown.</summary>
    private void RemoveForeignIframes(object? sender, PostProcessNodeEventArgs e)
    {
        if (e.Node is not IElement element ||
            !string.Equals(element.LocalName, "iframe", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var source = element.GetAttribute("src");
        var allowed = Uri.TryCreate(source, UriKind.Absolute, out var uri) &&
                      uri.Scheme == Uri.UriSchemeHttps &&
                      _iframeHosts.Contains(uri.IdnHost);
        if (!allowed)
        {
            // Replacing the node with empty text removes it.
            e.ReplacementNodes.Add(e.Document.CreateTextNode(string.Empty));
        }
    }

    [GeneratedRegex(@"^data:image/(png|jpeg|gif|webp|avif);base64,", RegexOptions.IgnoreCase)]
    private static partial Regex BitmapDataUrl();
}
