using ZauberCMS.Core.Content.Models;

namespace ZauberCMS.Core.Content.Interfaces;

/// <summary>
/// Decides which page an address shows when no page has that URL, for example <c>/products/{sku}</c> showing one
/// product page for every SKU (Roadmap D55). Discovered like the other extension points: a public class in a scanned
/// assembly, built per lookup with constructor injection. Finders run in <see cref="SortOrder"/> order (lowest
/// first) after the normal URL lookup has found nothing and before the not-found page; the first that returns a
/// result wins. A finder that throws is logged and the next one runs.
/// </summary>
/// <remarks>
/// ZauberCMS serves only a live page through a finder: the page the result names is loaded again, published, not
/// in the recycle bin and not a block, with the includes the normal lookup gives a view (and its internal redirect
/// followed). A finder that names any other page is passed over and the next one runs. Results are not cached, so a
/// finder whose search is expensive caches it itself (<c>ICacheService</c>).
/// </remarks>
public interface IContentFinder
{
    /// <summary>Lower runs first; finders with the same value run in order of their type's full name.</summary>
    int SortOrder { get; }

    /// <summary>
    /// The page to show for <paramref name="context"/>'s address, or null to leave it to the next finder (and in the
    /// end the not-found page).
    /// </summary>
    Task<ContentFinderResult?> FindAsync(ContentFinderContext context, CancellationToken cancellationToken = default);
}
