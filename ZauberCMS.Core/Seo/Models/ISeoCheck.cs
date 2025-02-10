using HtmlAgilityPack;

namespace ZauberCMS.Core.Seo.Models;

public interface ISeoCheck
{
    string Name { get; }
    Task<SeoCheckResult> Check(string url, HtmlDocument document, Content.Models.Content content);
    int SortOrder { get; }
}