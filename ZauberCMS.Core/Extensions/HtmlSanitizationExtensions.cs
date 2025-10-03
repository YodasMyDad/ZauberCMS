using Microsoft.AspNetCore.Components;
using ZauberCMS.Core.Shared.Services;

namespace ZauberCMS.Core.Extensions;

/// <summary>
/// Extension methods for HTML sanitization
/// </summary>
public static class HtmlSanitizationExtensions
{
    /// <summary>
    /// Sanitizes HTML content and returns it as a MarkupString for safe rendering in Blazor.
    /// Use this in your Blazor components when displaying rich text content.
    /// </summary>
    /// <param name="sanitizer">The HTML sanitizer service</param>
    /// <param name="html">The HTML to sanitize</param>
    /// <returns>A MarkupString containing sanitized HTML</returns>
    public static MarkupString SanitizeToMarkup(this IHtmlSanitizerService sanitizer, string? html)
    {
        var sanitized = sanitizer.Sanitize(html);
        return new MarkupString(sanitized);
    }
    
    /// <summary>
    /// Sanitizes HTML content asynchronously and returns it as a MarkupString.
    /// </summary>
    /// <param name="sanitizer">The HTML sanitizer service</param>
    /// <param name="html">The HTML to sanitize</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A MarkupString containing sanitized HTML</returns>
    public static async Task<MarkupString> SanitizeToMarkupAsync(this IHtmlSanitizerService sanitizer, string? html, CancellationToken cancellationToken = default)
    {
        var sanitized = await sanitizer.SanitizeAsync(html, cancellationToken);
        return new MarkupString(sanitized);
    }
}

