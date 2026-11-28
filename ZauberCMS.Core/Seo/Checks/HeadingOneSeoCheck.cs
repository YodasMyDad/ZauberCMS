using HtmlAgilityPack;
using Microsoft.Extensions.Localization;
using ZauberCMS.Core.Localization;
using ZauberCMS.Core.Seo.Models;
using ZauberCMS.Core.Shared.Models;

namespace ZauberCMS.Core.Seo.Checks;

public class HeadingOneSeoCheck(IStringLocalizer<ZauberText> text) : ISeoCheck
{
    public string Name => text["seo_headingOneCheck"];
    public Task<SeoCheckResult> Check(string url, HtmlDocument document, Content.Models.Content content)
    {
        var result = new SeoCheckResult(Name);
        var seoItem = new SeoCheckResultItem();

        // Select all H1 elements
        var h1Tags = document.DocumentNode.SelectNodes("//h1");

        if (h1Tags == null || h1Tags.Count == 0)
        {
            seoItem.Status = AlertType.Error;
            seoItem.DefaultMessage = text["seo_headingOneMissing"];
            result.Items.Add(seoItem);
        }
        else if (h1Tags.Count > 1)
        {
            seoItem.Status = AlertType.Warning;
            seoItem.DefaultMessage = text["seo_headingOneMultiple", h1Tags.Count];
            result.Items.Add(seoItem);
        }

        if (result.Items.Count == 0)
        {
            seoItem.Status = AlertType.Success;
            seoItem.DefaultMessage = text["seo_headingOnePresent"];
            result.Items.Add(seoItem);
        }

        return Task.FromResult(result);
    }
    
    public int SortOrder => 1;
}