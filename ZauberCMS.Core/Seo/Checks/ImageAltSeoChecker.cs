using HtmlAgilityPack;
using ZauberCMS.Core.Extensions;
using ZauberCMS.Core.Seo.Models;
using ZauberCMS.Core.Shared.Models;

namespace ZauberCMS.Core.Seo.Checks;

public class ImageAltSeoChecker : ISeoCheck
{
    public string Name => "Image Alt Checker";
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
            seoItem.DefaultMessage = $"{imagesWithoutAlt} image(s) are missing alt attributes.";
        }

        if (seoItem.Status == AlertType.Success)
        {
            seoItem.DefaultMessage = "All images have alt attributes.";
        }

        result.Items.Add(seoItem);
        
        return Task.FromResult(result);
    }
    public int SortOrder => 5;
}