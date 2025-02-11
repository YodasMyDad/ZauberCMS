using HtmlAgilityPack;
using ZauberCMS.Core.Seo.Models;

namespace ZauberCMS.Core.Seo.Checks;

public class MetaDescriptionSeoCheck : ISeoCheck
{
    public string Name => "Meta Description Checker";
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
            seoItem.Status = SeoCheckStatus.Error;
            seoItem.Message = "Meta description is missing.";
        }
        else if (metaDescription.Length is < 150 or > 160)
        {
            seoItem.Status = SeoCheckStatus.Warning;
            seoItem.Message = "Meta description should ideally be between 150 and 160 characters.";
        }
        
        if (seoItem.Status == SeoCheckStatus.Success)
        {
            seoItem.Message = "Meta description is fine";
        }

        result.Items.Add(seoItem);
        
        return Task.FromResult(result);
    }

    public int SortOrder => 1;
}