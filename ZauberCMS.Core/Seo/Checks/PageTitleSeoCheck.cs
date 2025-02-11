using HtmlAgilityPack;
using ZauberCMS.Core.Extensions;
using ZauberCMS.Core.Seo.Models;

namespace ZauberCMS.Core.Seo.Checks;

public class PageTitleSeoCheck : ISeoCheck
{
    public string Name => "Page Title Checker";
    
    public Task<SeoCheckResult> Check(string url, HtmlDocument document, Content.Models.Content content)
    {
        var result = new SeoCheckResult(Name);
        
        var title = document.DocumentNode.SelectSingleNode("//title")?.InnerText;

        var seoItem = new SeoCheckResultItem();
        
        if (title.IsNullOrWhiteSpace())
        {
            seoItem.Status = SeoCheckStatus.Error;
            seoItem.Message = "Page title is missing.";
        }
        
        // Check title length
        if (title is { Length: < 30 or > 60 })
        {
            seoItem.Status = SeoCheckStatus.Warning;
            seoItem.Message = "Page title length should be between 30 and 60 characters.";
        }

        if (seoItem.Status == SeoCheckStatus.Success)
        {
            seoItem.Message = title;
        }
        
        result.Items.Add(seoItem);
        
        return Task.FromResult(result);
    }

    public int SortOrder => 1;
}