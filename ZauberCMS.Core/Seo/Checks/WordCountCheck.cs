using ZauberCMS.Core.Seo.Models;
using HtmlAgilityPack;
using System.Text.RegularExpressions;
using ZauberCMS.Core.Shared.Models;

namespace ZauberCMS.Core.Seo.Checks;


public partial class WordCountCheck : ISeoCheck
{
    public string Name => "Word Count Check";

    public Task<SeoCheckResult> Check(string url, HtmlDocument document, Content.Models.Content content)
    {
        var result = new SeoCheckResult(Name);
        var seoItem = new SeoCheckResultItem();

        // Extract text content from the body
        var bodyNode = document.DocumentNode.SelectSingleNode("//body");
        var textContent = bodyNode?.InnerText ?? "";

        // Count words (ignoring extra spaces and special characters)
        var wordCount = CountWords(textContent);

        if (wordCount < 250)
        {
            seoItem.Status = AlertType.Error;
            seoItem.DefaultMessage = $"Page contains only {wordCount} words. Consider adding more content for better SEO.";
        }
        else if (wordCount < 500)
        {
            seoItem.Status = AlertType.Warning;
            seoItem.DefaultMessage = $"Page contains {wordCount} words. While acceptable, more content may improve SEO.";
        }
        else
        {
            return Task.FromResult(result); // No issues, don't add anything
        }

        result.Items.Add(seoItem);
        return Task.FromResult(result);
    }

    public int SortOrder => -9;

    private static int CountWords(string text)
    {
        // Normalize whitespace and count words
        return MyRegex().Matches(text).Count;
    }

    [GeneratedRegex(@"\b\w{2,}\b")]
    private static partial Regex MyRegex();
}
