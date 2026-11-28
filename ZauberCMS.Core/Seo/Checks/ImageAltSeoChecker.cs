using HtmlAgilityPack;
using Microsoft.Extensions.Localization;
using ZauberCMS.Core.Extensions;
using ZauberCMS.Core.Localization;
using ZauberCMS.Core.Seo.Models;
using ZauberCMS.Core.Shared.Models;

namespace ZauberCMS.Core.Seo.Checks;

public class ImageAltSeoChecker(IStringLocalizer<ZauberText> text) : ISeoCheck
{
    public string Name => text["seo_imageAltCheck"];
    public Task<SeoCheckResult> Check(string url, HtmlDocument document, Content.Models.Content content)
    {
        var result = new SeoCheckResult(Name);
        
        var seoItem = new SeoCheckResultItem();
        
        // Check for images without alt attributes
        var imagesWithoutAlt = document.DocumentNode
            .SelectNodes("//img[not(@alt) or normalize-space(@alt)='']")
            ?.Count;

        if (imagesWithoutAlt > 0)
        {
            seoItem.Status = AlertType.Warning;
            seoItem.DefaultMessage = text["seo_imagesMissingAlt", imagesWithoutAlt ?? 0];
        }

        if (seoItem.Status == AlertType.Success)
        {
            seoItem.DefaultMessage = text["seo_imagesAllHaveAlt"];
        }

        result.Items.Add(seoItem);
        
        return Task.FromResult(result);
    }
    public int SortOrder => 5;
}