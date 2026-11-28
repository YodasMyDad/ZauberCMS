using HtmlAgilityPack;
using Microsoft.Extensions.Localization;
using ZauberCMS.Core.Localization;
using ZauberCMS.Core.Seo.Models;
using ZauberCMS.Core.Shared.Models;

namespace ZauberCMS.Core.Seo.Checks;

public class CanonicalSeoChecker(IStringLocalizer<ZauberText> text) : ISeoCheck
{
    public string Name => text["seo_canonicalLinkCheck"];
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
            seoItem.DefaultMessage = text["seo_canonicalMissing"];
        }

        if (seoItem.Status == AlertType.Success)
        {
            seoItem.DefaultMessage = text["seo_canonicalPresent"];
        }

        result.Items.Add(seoItem);
        
        return Task.FromResult(result);
    }

    public int SortOrder => 5;
}