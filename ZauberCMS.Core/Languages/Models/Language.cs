using ZauberCMS.Core.Content.Models;
using ZauberCMS.Core.Extensions;

namespace ZauberCMS.Core.Languages.Models;

public class Language
{
    /// <summary>
    /// The ID
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid().NewSequentialGuid();
    
    /// <summary>
    /// Language ISO code
    /// </summary>
    public string? LanguageIsoCode { get; set; }
    
    /// <summary>
    /// Language culture name
    /// </summary>
    public string? LanguageCultureName { get; set; }
    
    /// <summary>
    /// The date and time when the item was created.
    /// </summary>
    public DateTime DateCreated { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Domains using this language
    /// </summary>
    public List<Domain> Domains { get; set; } = [];
}