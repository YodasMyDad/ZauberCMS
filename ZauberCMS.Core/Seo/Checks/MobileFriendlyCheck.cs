﻿using HtmlAgilityPack;
using ZauberCMS.Core.Seo.Models;
using ZauberCMS.Core.Shared.Models;

namespace ZauberCMS.Core.Seo.Checks;

public class MobileFriendlyCheck : ISeoCheck
{
    public string Name => "Mobile-Friendliness Check";

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
            seoItem.DefaultMessage = "Missing <meta name=\"viewport\"> tag. The page may not be mobile-friendly.";
        }
        else
        {
            var contentAttr = viewportMeta.GetAttributeValue("content", "").ToLower();
            
            // Recommended values for mobile-friendly pages
            const string recommended = "width=device-width, initial-scale=1";
            if (contentAttr.Contains("width=device-width") && contentAttr.Contains("initial-scale"))
            {
                seoItem.Status = AlertType.Success;
                seoItem.DefaultMessage = $"Viewport meta tag found: {contentAttr}";
            }
            else
            {
                seoItem.Status = AlertType.Warning;
                seoItem.DefaultMessage = $"Viewport tag detected but may not be fully optimized: {contentAttr}. Recommended: \"{recommended}\"";
            }
        }

        result.Items.Add(seoItem);
        return Task.FromResult(result);
    }

    public int SortOrder => 9;
}