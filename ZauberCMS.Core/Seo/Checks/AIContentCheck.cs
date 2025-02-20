using System.Text.RegularExpressions;
using HtmlAgilityPack;
using ZauberCMS.Core.Seo.Models;
using ZauberCMS.Core.Shared.Models;

namespace ZauberCMS.Core.Seo.Checks;

public partial class AiContentCheck : ISeoCheck
{
    public string Name => "AI Content Analysis";

    private readonly List<string> _aiTriggers =
    [
        "delve into", "intricate", "interplay", "embark", "embrace", "enrich", "pivotal",
        "sheds light", "paves the way", "underscore", "grasp", "unveil", "nuanced", "navigate",
        "poised", "revolutionize", "multifaceted", "showcase", "propel to the forefront",
        "paramount", "tapestry", "resonate", "intriguing", "realm", "transformative", "explore",
        "landscape", "dive into", "illuminate", "foster", "echo", "drawing from", "boast",
        "vibrant", "captivate", "testament", "shape", "clearly", "obviously", "ultimately",
        "simply put", "in conclusion", "to be honest", "needless to say", "as we can see",
        "in other words", "in essence", "basically", "fundamentally", "notably", "certainly",
        "naturally", "of course", "evidently", "specifically", "generally speaking",
        "for the most part", "as a matter of fact", "furthermore", "moreover", "additionally",
        "however", "therefore", "thus", "consequently", "nevertheless", "albeit", "hence",
        "inherently", "precisely", "predominantly", "respectively", "regarding", "pertaining to",
        "in terms of", "with respect to", "in regards to", "in relation to", "in this context",
        "it goes without saying", "it is worth noting", "it should be noted", "it is important to note",
        "it is interesting to note", "it is crucial to understand", "it is essential to remember",
        "this means that", "that being said", "in this case", "in light of", "as mentioned earlier",
        "as discussed above", "as previously mentioned", "as we discussed", "moving forward",
        "let me help", "i'd be happy to", "i apologize", "rest assured", "feel free to",
        "i understand", "indeed", "utilize", "leverage", "facilitate", "optimal", "robust",
        "streamline", "seamless", "interface", "synergy", "paradigm", "framework", "methodology",
        "implementation", "functionality", "infrastructure"
    ];

    public Task<SeoCheckResult> Check(string url, HtmlDocument document, Content.Models.Content content)
    {
        var result = new SeoCheckResult(Name);
        var seoItem = new SeoCheckResultItem();

        var bodyText = document.DocumentNode.InnerText;

        // Pattern Analysis: Repeated Phrases Check
        var repeatedPhrases = _aiTriggers.Where(trigger => Regex.Matches(bodyText, $@"\b{Regex.Escape(trigger)}\b", RegexOptions.IgnoreCase).Count > 2).ToList();
        
        // Language Flow Analysis: Sentence Length Consistency Check
        var sentences = MyRegex().Split(bodyText).Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
        var averageSentenceLength = sentences.Average(s => s.Length);
        var shortSentenceCount = sentences.Count(s => s.Length < averageSentenceLength * 0.5);
        var uniformityScore = shortSentenceCount / (double)sentences.Count;

        // Context Coherence Check (Repetitive Transitions)
        var transitions = new List<string> { "however", "moreover", "therefore", "thus", "consequently", "furthermore" };
        var transitionCount = transitions.Sum(t => Regex.Matches(bodyText, $@"\b{t}\b", RegexOptions.IgnoreCase).Count);
        var transitionDensity = transitionCount / (double)sentences.Count;

        var analysisSummary = (repeatedPhrases.Count > 0 ? $" - Repeated AI Phrases: {string.Join(", ", repeatedPhrases)}\n" : string.Empty)
                      + $" - Average Sentence Length: {averageSentenceLength:F2} characters"
                      + $" - Short Sentence Ratio: {uniformityScore:P2}"
                      + $" - Transition Density: {transitionDensity:P2}";

        if (repeatedPhrases.Count > 5 || uniformityScore < 0.2 || transitionDensity > 0.1)
        {
            seoItem.Status = AlertType.Warning;
            seoItem.Message = $"Potential AI-Generated Content Detected{analysisSummary}";
        }
        else
        {
            seoItem.Status = AlertType.Success;
            seoItem.Message = $"Content appears natural{analysisSummary}";
        }

        result.Items.Add(seoItem);
        return Task.FromResult(result);
    }

    public int SortOrder => -10;

    [GeneratedRegex("[.!?]")]
    private static partial Regex MyRegex();
}
