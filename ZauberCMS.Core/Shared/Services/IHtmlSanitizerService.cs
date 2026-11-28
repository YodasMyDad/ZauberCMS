namespace ZauberCMS.Core.Shared.Services;

/// <summary>
/// Service for sanitizing HTML content to prevent XSS attacks.
/// Developers can implement this interface to provide custom sanitization logic.
/// </summary>
public interface IHtmlSanitizerService
{
    /// <summary>
    /// Sanitizes the provided HTML string by removing potentially dangerous content. The result is for the page it is
    /// rendered on (the default one puts the request's path base in front of page links, Roadmap D141): sanitise stored
    /// HTML once, when it is rendered, and don't store or sanitise the result again.
    /// </summary>
    /// <param name="html">The HTML string to sanitize</param>
    /// <returns>The sanitized HTML string</returns>
    string Sanitize(string? html);
    
    /// <summary>
    /// Sanitizes the provided HTML string asynchronously. Override this for custom async logic.
    /// </summary>
    /// <param name="html">The HTML string to sanitize</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The sanitized HTML string</returns>
    Task<string> SanitizeAsync(string? html, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Sanitize(html));
    }
}

