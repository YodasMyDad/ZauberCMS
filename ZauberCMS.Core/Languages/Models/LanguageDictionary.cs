using ZauberCMS.Core.Extensions;

namespace ZauberCMS.Core.Languages.Models;

public class LanguageDictionary
{
    /// <summary>
    /// The ID
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid().NewSequentialGuid();
    
    /// <summary>
    /// The text key for the language text
    /// </summary>
    public string Key { get; set; } = string.Empty;
    
    /// <summary>
    /// Child language texts
    /// </summary>
    public List<LanguageText> Texts { get; set; } = [];
}