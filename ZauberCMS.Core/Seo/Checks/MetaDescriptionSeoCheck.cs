using HtmlAgilityPack;
using ZauberCMS.Core.Seo.Models;
using ZauberCMS.Core.Shared.Models;

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
            seoItem.Status = AlertType.Error;
            seoItem.DefaultMessage = "Meta description is missing.";
        }
        else if (metaDescription.Length is < 150 or > 160)
        {
            seoItem.Status = AlertType.Warning;
            seoItem.DefaultMessage = "Meta description should ideally be between 150 and 160 characters.";
        }
        
        if (seoItem.Status == AlertType.Success)
        {
            seoItem.DefaultMessage = "Meta description is fine";
        }

        result.Items.Add(seoItem);
        
        return Task.FromResult(result);
    }

    public int SortOrder => 1;
}