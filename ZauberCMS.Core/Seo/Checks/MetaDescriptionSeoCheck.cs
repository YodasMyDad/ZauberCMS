using HtmlAgilityPack;
using ZauberCMS.Core.Seo.Models;

namespace ZauberCMS.Core.Seo.Checks;

public class MetaDescriptionSeoCheck : ISeoCheck
{
    public string Name => "Meta Description Checker";
    public Task<SeoCheckResult> Check(string url, HtmlDocument document, Content.Models.Content content)
    {
        var result = new SeoCheckResult(Name);
        
        // Check for meta description
        var metaDescription = document.DocumentNode
            .SelectSingleNode("//meta[@name='description']")?
            .GetAttributeValue("content", string.Empty);

        if (string.IsNullOrWhiteSpace(metaDescription))
        {
            result.Status = SeoCheckStatus.Error;
            result.Message = "Meta description is missing.";
        }
        else if (metaDescription.Length is < 150 or > 160)
        {
            result.Status = SeoCheckStatus.Warning;
            result.Message = "Meta description should ideally be between 150 and 160 characters.";
        }
        
        if (result.Status == SeoCheckStatus.Success)
        {
            result.Message = "Meta description is fine";
        }

        
        return Task.FromResult(result);
    }

    public int SortOrder => 1;
}