namespace ZauberCMS.Core.Content.Interfaces;

public interface IContentView
{
    /// <summary>
    /// The Content that the View uses
    /// </summary>
    Models.Content? Content { get; set; }
    
    /// <summary>
    /// Language dicts for the currently set language
    /// </summary>
    Dictionary<string, string>? LanguageKeys { get; set; }
}