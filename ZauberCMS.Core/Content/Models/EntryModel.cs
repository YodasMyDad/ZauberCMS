namespace ZauberCMS.Core.Content.Models;

public class EntryModel
{
    public Content? Content { get; set; }
    public Dictionary<string, string> LanguageKeys { get; set; } = [];
    public string? LanguageIsoCode { get; set; }
}