using System.Text.RegularExpressions;
using HtmlAgilityPack;
using ZauberCMS.Core.Seo.Models;

namespace ZauberCMS.Core.Seo.Checks;

public partial class TitleH1KeywordMatchCheck : ISeoCheck
{
    public string Name => "Keyword Match Check";

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
            seoItem.Status = SeoCheckStatus.Warning;
            seoItem.Message = "Either the page title or the <h1> is missing, making keyword consistency impossible.";
            result.Items.Add(seoItem);
        }
        else
        {
            // Extract meaningful words from title and H1
            var titleWords = ExtractSignificantWords(titleText);
            var h1Words = ExtractSignificantWords(h1Text);

            // Find matching words
            var matchingWords = titleWords.Intersect(h1Words, StringComparer.OrdinalIgnoreCase).ToList();

            if (!matchingWords.Any())
            {
                seoItem.Status = SeoCheckStatus.Warning;
                seoItem.Message = "No keywords from the title appear in the <h1>. Consider aligning them for better SEO.";
                result.Items.Add(seoItem);
            }
        }

        if (result.Items.Count == 0)
        {
            seoItem.Status = SeoCheckStatus.Success;
            seoItem.Message = "Page title and <h1> contain keywords words that match.";
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