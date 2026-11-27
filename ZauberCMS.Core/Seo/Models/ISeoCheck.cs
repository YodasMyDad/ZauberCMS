using HtmlAgilityPack;

namespace ZauberCMS.Core.Seo.Models;

public interface ISeoCheck
{
    string Name { get; }
    Task<SeoCheckResult> Check(string url, HtmlDocument document, Content.Models.Content content);

    /// <summary>
    /// The check, stopped when <paramref name="cancellationToken"/> is cancelled: the SEO checker cancels it when the
    /// editor leaves the page, runs the checks again or the run takes too long (ZA-085). A check that makes requests
    /// or waits should implement this one; the default calls <see cref="Check(string, HtmlDocument, Content.Models.Content)"/>.
    /// </summary>
    Task<SeoCheckResult> Check(string url, HtmlDocument document, Content.Models.Content content,
        CancellationToken cancellationToken) => Check(url, document, content);

    int SortOrder { get; }
}
