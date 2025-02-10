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

        if (title.IsNullOrWhiteSpace())
        {
            result.Status = SeoCheckStatus.Error;
            result.Message = "Page title is missing.";
        }
        
        // Check title length
        if (title is { Length: < 30 or > 60 })
        {
            result.Status = SeoCheckStatus.Warning;
            result.Message = "Page title length should be between 30 and 60 characters.";
        }

        if (result.Status == SeoCheckStatus.Success)
        {
            result.Message = title;
        }
        
        return Task.FromResult(result);
    }

    public int SortOrder => 1;
}