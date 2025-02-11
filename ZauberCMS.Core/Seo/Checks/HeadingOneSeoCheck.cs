using HtmlAgilityPack;
using ZauberCMS.Core.Extensions;
using ZauberCMS.Core.Seo.Models;

namespace ZauberCMS.Core.Seo.Checks;

public class HeadingOneSeoCheck : ISeoCheck
{
    public string Name => "H1 Checker";
    public Task<SeoCheckResult> Check(string url, HtmlDocument document, Content.Models.Content content)
    {
        var result = new SeoCheckResult(Name);
        var seoItem = new SeoCheckResultItem();

        // Select all H1 elements
        var h1Tags = document.DocumentNode.SelectNodes("//h1");

        if (h1Tags == null || h1Tags.Count == 0)
        {
            seoItem.Status = SeoCheckStatus.Error;
            seoItem.Message = "Page is missing an <h1> heading tag.";
        }
        else if (h1Tags.Count > 1)
        {
            seoItem.Status = SeoCheckStatus.Warning;
            seoItem.Message = $"Page contains multiple <h1> tags ({h1Tags.Count}). Consider reducing to one for better SEO.";
        }
        else
        {
            seoItem.Status = SeoCheckStatus.Success;
            seoItem.Message = $"H1 found: '{h1Tags[0].InnerText.Trim()}'.";
        }

        result.Items.Add(seoItem);
        return Task.FromResult(result);
    }
    
    public int SortOrder => 1;
}