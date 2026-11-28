using ZauberCMS.Core.Seo.Models;
using HtmlAgilityPack;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Localization;
using ZauberCMS.Core.Localization;
using ZauberCMS.Core.Shared.Models;

namespace ZauberCMS.Core.Seo.Checks;


public partial class WordCountCheck(IStringLocalizer<ZauberText> text) : ISeoCheck
{
    public string Name => text["seo_wordCountCheck"];

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
            seoItem.DefaultMessage = text["seo_wordCountLow", wordCount];
        }
        else if (wordCount < 500)
        {
            seoItem.Status = AlertType.Warning;
            seoItem.DefaultMessage = text["seo_wordCountFair", wordCount];
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
        return MyRegex().Count(text);
    }

    [GeneratedRegex(@"\b\w{2,}\b")]
    private static partial Regex MyRegex();
}
