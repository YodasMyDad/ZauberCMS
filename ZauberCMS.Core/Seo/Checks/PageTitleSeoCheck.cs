using HtmlAgilityPack;
using Microsoft.Extensions.Localization;
using ZauberCMS.Core.Extensions;
using ZauberCMS.Core.Localization;
using ZauberCMS.Core.Seo.Models;
using ZauberCMS.Core.Shared.Models;

namespace ZauberCMS.Core.Seo.Checks;

public class PageTitleSeoCheck(IStringLocalizer<ZauberText> text) : ISeoCheck
{
    public string Name => text["seo_pageTitleCheck"];
    
    public Task<SeoCheckResult> Check(string url, HtmlDocument document, Content.Models.Content content)
    {
        var result = new SeoCheckResult(Name);
        
        var title = document.DocumentNode.SelectSingleNode("//title")?.InnerText;

        var seoItem = new SeoCheckResultItem();
        
        if (title.IsNullOrWhiteSpace())
        {
            seoItem.Status = AlertType.Error;
            seoItem.DefaultMessage = text["seo_pageTitleMissing"];
        }
        
        // Check title length
        if (title is { Length: < 30 or > 60 })
        {
            seoItem.Status = AlertType.Warning;
            seoItem.DefaultMessage = text["seo_pageTitleLength"];
        }

        if (seoItem.Status == AlertType.Success)
        {
            seoItem.DefaultMessage = title;
        }
        
        result.Items.Add(seoItem);
        
        return Task.FromResult(result);
    }

    public int SortOrder => 1;
}