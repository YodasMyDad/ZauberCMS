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
        
        // Check for H1
        var h1 = document.DocumentNode.SelectSingleNode("//h1");
        if (h1 == null || h1.InnerText.IsNullOrWhiteSpace())
        {
            seoItem.Status = SeoCheckStatus.Error;
            seoItem.Message = "Page is missing an <h1> heading tag.";
        }

        if (seoItem.Status == SeoCheckStatus.Success)
        {
            seoItem.Message = h1?.InnerText;
        }
        
        result.Items.Add(seoItem);
        
        return Task.FromResult(result);
    }
    
    public int SortOrder => 1;
}