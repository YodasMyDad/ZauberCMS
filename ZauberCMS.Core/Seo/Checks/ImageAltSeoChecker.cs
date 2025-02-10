using HtmlAgilityPack;
using ZauberCMS.Core.Extensions;
using ZauberCMS.Core.Seo.Models;

namespace ZauberCMS.Core.Seo.Checks;

public class ImageAltSeoChecker : ISeoCheck
{
    public string Name => "Image Alt Checker";
    public Task<SeoCheckResult> Check(string url, HtmlDocument document, Content.Models.Content content)
    {
        var result = new SeoCheckResult(Name);
        
        // Check for images without alt attributes
        var imagesWithoutAlt = document.DocumentNode
            .SelectNodes("//img[not(@alt) or normalize-space(@alt)='']")
            ?.Count;

        if (imagesWithoutAlt > 0)
        {
            result.Status = SeoCheckStatus.Warning;
            result.Message = $"{imagesWithoutAlt} image(s) are missing alt attributes.";
        }

        if (result.Status == SeoCheckStatus.Success)
        {
            result.Message = "All images have alt attributes.";
        }

        return Task.FromResult(result);
    }
    public int SortOrder => 5;
}