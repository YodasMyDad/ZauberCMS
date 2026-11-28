using HtmlAgilityPack;
using Microsoft.Extensions.Localization;
using ZauberCMS.Core.Localization;
using ZauberCMS.Core.Seo.Models;
using ZauberCMS.Core.Shared.Models;

namespace ZauberCMS.Core.Seo.Checks;

public class MobileFriendlyCheck(IStringLocalizer<ZauberText> text) : ISeoCheck
{
    public string Name => text["seo_mobileFriendlyCheck"];

    public Task<SeoCheckResult> Check(string url, HtmlDocument document, Content.Models.Content content)
    {
        var result = new SeoCheckResult(Name);
        var seoItem = new SeoCheckResultItem();

        // Find the viewport meta tag
        var viewportMeta = document.DocumentNode
            .SelectSingleNode("//meta[@name='viewport']");

        if (viewportMeta == null)
        {
            seoItem.Status = AlertType.Error;
            seoItem.DefaultMessage = text["seo_viewportMissing"];
        }
        else
        {
            var contentAttr = viewportMeta.GetAttributeValue("content", "").ToLowerInvariant();
            
            // Recommended values for mobile-friendly pages
            const string recommended = "width=device-width, initial-scale=1";
            if (contentAttr.Contains("width=device-width") && contentAttr.Contains("initial-scale"))
            {
                seoItem.Status = AlertType.Success;
                seoItem.DefaultMessage = text["seo_viewportFound", contentAttr];
            }
            else
            {
                seoItem.Status = AlertType.Warning;
                seoItem.DefaultMessage = text["seo_viewportNotOptimized", contentAttr, recommended];
            }
        }

        result.Items.Add(seoItem);
        return Task.FromResult(result);
    }

    public int SortOrder => 9;
}