using HtmlAgilityPack;
using Microsoft.Extensions.Localization;
using ZauberCMS.Core.Localization;
using ZauberCMS.Core.Seo.Models;
using ZauberCMS.Core.Shared.Models;

namespace ZauberCMS.Core.Seo.Checks;

public class MetaDescriptionSeoCheck(IStringLocalizer<ZauberText> text) : ISeoCheck
{
    public string Name => text["seo_metaDescriptionCheck"];
    public Task<SeoCheckResult> Check(string url, HtmlDocument document, Content.Models.Content content)
    {
        var result = new SeoCheckResult(Name);
        
        var seoItem = new SeoCheckResultItem();
        
        // Check for meta description
        var metaDescription = document.DocumentNode
            .SelectSingleNode("//meta[@name='description']")?
            .GetAttributeValue("content", string.Empty);

        if (string.IsNullOrWhiteSpace(metaDescription))
        {
            seoItem.Status = AlertType.Error;
            seoItem.DefaultMessage = text["seo_metaDescriptionMissing"];
        }
        else if (metaDescription.Length is < 150 or > 160)
        {
            seoItem.Status = AlertType.Warning;
            seoItem.DefaultMessage = text["seo_metaDescriptionLength"];
        }
        
        if (seoItem.Status == AlertType.Success)
        {
            seoItem.DefaultMessage = text["seo_metaDescriptionFine"];
        }

        result.Items.Add(seoItem);
        
        return Task.FromResult(result);
    }

    public int SortOrder => 1;
}