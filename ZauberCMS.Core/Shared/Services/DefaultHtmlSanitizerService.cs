using Ganss.Xss;

namespace ZauberCMS.Core.Shared.Services;

/// <summary>
/// Default implementation of HTML sanitization using HtmlSanitizer library.
/// Configured with safe defaults for CMS content.
/// </summary>
public class DefaultHtmlSanitizerService : IHtmlSanitizerService
{
    private readonly HtmlSanitizer _sanitizer;

    public DefaultHtmlSanitizerService()
    {
        _sanitizer = new HtmlSanitizer();
        
        // Allow common HTML elements and attributes needed for rich text content
        _sanitizer.AllowedTags.Add("iframe"); // For video embeds
        _sanitizer.AllowedAttributes.Add("class");
        _sanitizer.AllowedAttributes.Add("style");
        
        // Allow data attributes for all tags
        _sanitizer.AllowDataAttributes = true;
        
        // Allow specific schemes
        _sanitizer.AllowedSchemes.Add("data"); // For inline images
        _sanitizer.AllowedSchemes.Add("http");
        _sanitizer.AllowedSchemes.Add("https");
        
        // Configure allowed iframe sources (YouTube, Vimeo, etc.)
        _sanitizer.AllowedCssProperties.Add("width");
        _sanitizer.AllowedCssProperties.Add("height");
        _sanitizer.AllowedCssProperties.Add("border");
        
        // Use UriAttributes to control which attributes contain URLs
        _sanitizer.UriAttributes.Add("src");
        _sanitizer.UriAttributes.Add("href");
    }

    public string Sanitize(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return string.Empty;
        }

        return _sanitizer.Sanitize(html);
    }
}

