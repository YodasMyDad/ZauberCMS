using HtmlAgilityPack;
using ZauberCMS.RTE.Models;

namespace ZauberCMS.Core.Shared.Services;

/// <summary>
/// Default <see cref="IRichTextEmbedService"/>: YouTube and Vimeo, rendered as
/// <c>&lt;figure class="zauber-embed" data-embed="…"&gt;&lt;iframe …&gt;&lt;/iframe&gt;&lt;/figure&gt;</c>.
/// </summary>
public class DefaultRichTextEmbedService : IRichTextEmbedService
{
    /// <summary>The class on the <c>figure</c> around every rendered embed, for the site's own CSS.</summary>
    public const string EmbedCssClass = "zauber-embed";

    private const string ProviderAttribute = "data-embed";
    private const string IdAttribute = "data-embed-id";

    /// <inheritdoc />
    public virtual IReadOnlyList<EmbedProvider> Providers => EmbedProvider.Defaults;

    /// <inheritdoc />
    public virtual string Expand(string? html)
    {
        if (string.IsNullOrEmpty(html))
        {
            return string.Empty;
        }

        // Most rich text has no embed, so it is not parsed.
        if (!html.Contains(ProviderAttribute, StringComparison.OrdinalIgnoreCase))
        {
            return html;
        }

        var document = new HtmlDocument();
        document.LoadHtml(html);

        var placeholders = document.DocumentNode.SelectNodes($"//figure[@{ProviderAttribute}]");
        if (placeholders == null)
        {
            return html;
        }

        var providers = Providers;
        foreach (var placeholder in placeholders)
        {
            // Already expanded (a figure that holds its player): leave it alone, so expanding twice is harmless.
            if (placeholder.SelectSingleNode("./iframe") != null)
            {
                continue;
            }

            var provider = EmbedProvider.Find(providers, placeholder.GetAttributeValue(ProviderAttribute, string.Empty));
            var id = placeholder.GetAttributeValue(IdAttribute, string.Empty);
            if (provider == null || !EmbedProvider.IsValidId(id))
            {
                placeholder.Remove();
                continue;
            }

            placeholder.ParentNode?.ReplaceChild(HtmlNode.CreateNode(BuildEmbedHtml(provider, id)), placeholder);
        }

        return document.DocumentNode.OuterHtml;
    }

    /// <summary>
    /// The markup for one embed. <paramref name="id"/> has already been validated. The default gives the player
    /// the usual 560 × 315 size as attributes, which the site's CSS can override (for example
    /// <c>.zauber-embed iframe { width: 100%; height: auto; aspect-ratio: 16 / 9; }</c>).
    /// </summary>
    protected virtual string BuildEmbedHtml(EmbedProvider provider, string id)
    {
        var iframe = HtmlNode.CreateNode(provider.BuildIframeHtml(id));
        iframe.SetAttributeValue("width", "560");
        iframe.SetAttributeValue("height", "315");

        return $"<figure class=\"{EmbedCssClass}\" {ProviderAttribute}=\"{provider.Name}\">{iframe.OuterHtml}</figure>";
    }
}
