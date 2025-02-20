using HtmlAgilityPack;
using ZauberCMS.Core.Seo.Models;
using ZauberCMS.Core.Shared.Models;

namespace ZauberCMS.Core.Seo.Checks;

public class CanonicalSeoChecker : ISeoCheck
{
    public string Name => "Canonical Link Checker";
    public Task<SeoCheckResult> Check(string url, HtmlDocument document, Content.Models.Content content)
    {
        var result = new SeoCheckResult(Name);
        
        // Check if canonical link is missing
        var canonicalLinkExists = document.DocumentNode
            .SelectSingleNode("//link[@rel='canonical']") != null;

        var seoItem = new SeoCheckResultItem();
        
        if (!canonicalLinkExists)
        {
            seoItem.Status = AlertType.Warning;
            seoItem.Message = "Canonical link is missing.";
        }

        if (seoItem.Status == AlertType.Success)
        {
            seoItem.Message = "Canonical link is present.";
        }

        result.Items.Add(seoItem);
        
        return Task.FromResult(result);
    }

    public int SortOrder => 5;
}