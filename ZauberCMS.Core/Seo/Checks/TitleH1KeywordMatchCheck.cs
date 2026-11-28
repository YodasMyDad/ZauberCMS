using System.Text.RegularExpressions;
using HtmlAgilityPack;
using Microsoft.Extensions.Localization;
using ZauberCMS.Core.Localization;
using ZauberCMS.Core.Seo.Models;
using ZauberCMS.Core.Shared.Models;

namespace ZauberCMS.Core.Seo.Checks;

public partial class TitleH1KeywordMatchCheck(IStringLocalizer<ZauberText> text) : ISeoCheck
{
    public string Name => text["seo_keywordMatchCheck"];

    public Task<SeoCheckResult> Check(string url, HtmlDocument document, Content.Models.Content content)
    {
        var result = new SeoCheckResult(Name);
        var seoItem = new SeoCheckResultItem();

        // Extract title text
        var titleNode = document.DocumentNode.SelectSingleNode("//title");
        var titleText = titleNode?.InnerText.Trim() ?? "";

        // Extract H1 text
        var h1Node = document.DocumentNode.SelectSingleNode("//h1");
        var h1Text = h1Node?.InnerText.Trim() ?? "";

        if (string.IsNullOrWhiteSpace(titleText) || string.IsNullOrWhiteSpace(h1Text))
        {
            seoItem.Status = AlertType.Warning;
            seoItem.DefaultMessage = text["seo_keywordTitleOrHeadingMissing"];
            result.Items.Add(seoItem);
        }
        else
        {
            // Extract meaningful words from title and H1
            var titleWords = ExtractSignificantWords(titleText);
            var h1Words = ExtractSignificantWords(h1Text);

            // Find matching words
            var matchingWords = titleWords.Intersect(h1Words, StringComparer.OrdinalIgnoreCase).ToList();

            if (matchingWords.Count == 0)
            {
                seoItem.Status = AlertType.Warning;
                seoItem.DefaultMessage = text["seo_keywordNoMatch"];
                result.Items.Add(seoItem);
            }
        }

        if (result.Items.Count == 0)
        {
            seoItem.Status = AlertType.Success;
            seoItem.DefaultMessage = text["seo_keywordMatch"];
            result.Items.Add(seoItem);
        }
        
        return Task.FromResult(result);
    }

    public int SortOrder => 10;

    private static List<string> ExtractSignificantWords(string text)
    {
        // Define a regex to extract words, ignoring special characters and short words (1-2 chars)
        return MyRegex().Matches(text) // Matches words with 3+ characters
                    .Select(m => m.Value)
                    .Distinct()
                    .ToList();
    }

    [GeneratedRegex(@"\b[a-zA-Z]{3,}\b")]
    private static partial Regex MyRegex();
}