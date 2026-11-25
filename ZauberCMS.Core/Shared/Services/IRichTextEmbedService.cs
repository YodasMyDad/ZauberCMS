using ZauberCMS.RTE.Models;

namespace ZauberCMS.Core.Shared.Services;

/// <summary>
/// Video embeds in rich text. The editor stores an embed as a placeholder
/// (<c>&lt;figure data-embed="youtube" data-embed-id="…"&gt;</c>), never as an <c>&lt;iframe&gt;</c>; this service
/// owns the list of sites that may be embedded and turns placeholders into players when content is rendered.
/// Register your own implementation (or a subclass of <see cref="DefaultRichTextEmbedService"/>) after
/// <c>AddZauberCms</c> to add a provider or change the markup.
/// </summary>
public interface IRichTextEmbedService
{
    /// <summary>
    /// The sites that may be embedded. The rich text editor and <see cref="Expand"/> both use this list, so a
    /// provider added here can be inserted in the editor and is rendered on the site.
    /// </summary>
    IReadOnlyList<EmbedProvider> Providers { get; }

    /// <summary>
    /// Replaces each embed placeholder in <paramref name="html"/> with its player. A placeholder for an unknown
    /// provider, or with an invalid id, is removed. Everything else is returned unchanged.
    /// </summary>
    string Expand(string? html);
}
