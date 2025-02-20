using HtmlAgilityPack;
using ZauberCMS.Core.Seo.Models;
using ZauberCMS.Core.Shared.Models;

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
            seoItem.Status = AlertType.Error;
            seoItem.Message = "Page is missing an <h1> heading tag.";
            result.Items.Add(seoItem);
        }
        else if (h1Tags.Count > 1)
        {
            seoItem.Status = AlertType.Warning;
            seoItem.Message = $"Page contains multiple <h1> tags ({h1Tags.Count}). Consider reducing to one for better SEO.";
            result.Items.Add(seoItem);
        }

        if (result.Items.Count == 0)
        {
            seoItem.Status = AlertType.Success;
            seoItem.Message = "Page contains an <h1> heading tag.";
            result.Items.Add(seoItem);
        }

        return Task.FromResult(result);
    }
    
    public int SortOrder => 1;
}