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
        "additionally", "albeit", "alleviate", "alright", "also", "although", "amongst", "arguably",
        "assessing", "as a matter of fact", "as a professional", "as mentioned earlier",
        "as previously mentioned", "as such", "as well as", "balancing", "bear in mind", "because",
        "beacon", "buckle up", "bustling", "bombastic", "blank", "captivate", "certainly", "clearly",
        "competitive digital world", "comprehensive", "consequently", "crucial", "crucible",
        "curated", "cutting-edge", "dance", "daunting", "deep dive", "delve", "designed to enhance",
        "despite", "detailed", "digital world", "dive", "dive into", "drawing from", "due to", "eager",
        "echo", "efficiency", "elevate", "embrace", "embark", "emphasis", "emphasize", "enable",
        "enigma", "ensure", "enter AI", "enter Bard", "enter ChatGPT", "enter Claude", "enter Gemini",
        "enter Perplexity", "essential", "essentially", "evidently", "everchanging", "ever-evolving",
        "excels", "expanding", "explore", "extensive", "facilitate", "fancy", "feel free to", "field",
        "firstly", "folks", "foster", "fostering", "framework", "frontier", "fundamentally",
        "furthermore", "game changer", "generally", "generally speaking", "gemini", "given that",
        "grasp", "harness", "hence", "hey", "however", "hurdles", "hustle and bustle", "i apologize",
        "i'd be happy to", "i understand", "illuminate", "imagine", "implementation", "importantly",
        "in a nutshell", "in a world of", "in conclusion", "in contrast", "in essence", "in infrastructure",
        "in light of", "in order to", "in other words", "in regards to", "in relation to", "in summary",
        "in terms of", "in the realm", "in today's digital age", "in today's digital era",
        "in today's fast-paced", "in the world of", "in this case", "indeed", "indelible",
        "interestingly", "intricate", "intriguing", "it depends on", "it goes without saying",
        "it is advisable", "it is crucial to understand", "it is essential to remember",
        "it is important to consider", "it is important to note", "it is interesting to note",
        "it is worth noting", "it should be noted", "it’s worth noting that", "journey", "keen",
        "labyrinth", "labyrinthine", "landscape", "lastly", "leverage", "mastering", "metamorphosis",
        "meticulous", "meticulously", "methodology", "moist", "moreover", "moving forward",
        "multifaceted", "navigating", "navigate", "naturally", "needless to say", "nestled",
        "nevertheless", "notably", "noteworthy", "nuance", "nuanced", "obviously", "optimal",
        "out of the box", "paramount", "paradigm", "peace of mind", "pesky", "paves the way",
        "picture this", "pivot", "plethora", "poised", "power", "predominantly", "precisely",
        "propel to the forefront", "promptly", "realm", "recognize", "refreshing", "regarding",
        "relevance", "remember that", "remnant", "reverberate", "revolutionize", "robust", "shall",
        "shape", "shed light", "showcase", "significantly", "similarly", "simply put", "sights unseen",
        "soul", "specifically", "streamline", "strive", "striving", "subsequently", "sure", "synergy",
        "tailored", "tapestry", "testament", "that being said", "there are a few considerations",
        "therefore", "thus", "to be honest", "to consider", "to put it simply", "to sum up",
        "top-notch", "transformative", "transition", "transitioning", "treasure box", "treasure trove",
        "ultimately", "unleash", "unleashed", "unlimited guarantee", "unlock", "unlocked",
        "underscore", "underpins", "understanding", "unveiled", "unveil", "utilize", "vibrant", "vital",
        "we know", "we understand", "we've got you covered", "whimsical", "while", "whispering",
        "with respect to", "you may want to"
    ];

    public Task<SeoCheckResult> Check(string url, HtmlDocument document, Content.Models.Content content)
    {
        var result = new SeoCheckResult(Name);
        var seoItem = new SeoCheckResultItem();

        var bodyText = document.DocumentNode.InnerText;

        // Pattern Analysis: Repeated Phrases Check
        var repeatedPhrases = _aiTriggers.Where(trigger =>
            Regex.Matches(bodyText, $@"\b{Regex.Escape(trigger)}\b", RegexOptions.IgnoreCase).Count > 2).ToList();

        // Language Flow Analysis: Sentence Length Consistency Check
        var sentences = MyRegex().Split(bodyText).Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
        var averageSentenceLength = sentences.Average(s => s.Length);
        var shortSentenceCount = sentences.Count(s => s.Length < averageSentenceLength * 0.5);
        var uniformityScore = shortSentenceCount / (double)sentences.Count;

        // Context Coherence Check (Repetitive Transitions)
        var transitions = new List<string>
            { "however", "moreover", "therefore", "thus", "consequently", "furthermore" };
        var transitionCount = transitions.Sum(t => Regex.Matches(bodyText, $@"\b{t}\b", RegexOptions.IgnoreCase).Count);
        var transitionDensity = transitionCount / (double)sentences.Count;

        var analysisSummary = (repeatedPhrases.Count > 0
                                  ? $" - Repeated AI Phrases: {string.Join(", ", repeatedPhrases)}\n"
                                  : string.Empty)
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