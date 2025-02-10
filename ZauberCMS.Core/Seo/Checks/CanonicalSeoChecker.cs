using HtmlAgilityPack;
using ZauberCMS.Core.Seo.Models;

namespace ZauberCMS.Core.Seo.Checks;

public class CanonicalSeoChecker : ISeoCheck
{
    public string Name => "Canonical Link Checker";
    public Task<SeoCheckResult> Check(string url, HtmlDocument document, Content.Models.Content content)
    {
        var result = new SeoCheckResult(Name);
        
        // Check if canonical link is missing
        var canonicalLinkExists = document.DocumentNode
            .SelectSingleNode("//link[@rel='canonical']") != null;

        if (!canonicalLinkExists)
        {
            result.Status = SeoCheckStatus.Warning;
            result.Message = "Canonical link is missing.";
        }

        if (result.Status == SeoCheckStatus.Success)
        {
            result.Message = "Canonical link is present.";
        }

        return Task.FromResult(result);
    }

    public int SortOrder => 5;
}