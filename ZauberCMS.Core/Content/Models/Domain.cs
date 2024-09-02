using ZauberCMS.Core.Extensions;
using ZauberCMS.Core.Languages.Models;

namespace ZauberCMS.Core.Content.Models;

public class Domain
{
    /// <summary>
    /// The ID
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid().NewSequentialGuid();
    
    /// <summary>
    /// The content ID
    /// </summary>
    public Guid ContentId { get; set; }
    
    /// <summary>
    /// The domain name to match
    /// </summary>
    public string? Url { get; set; }
    
    /// <summary>
    /// Language associated with this domain
    /// </summary>
    public Guid LanguageId { get; set; }
    
    /// <summary>
    /// The Language object
    /// </summary>
    public Language? Language { get; set; }
    
    /// <summary>
    /// The date and time when the item was created.
    /// </summary>
    public DateTime DateCreated { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// The date and time the item was updated
    /// </summary>
    public DateTime DateUpdated { get; set; } = DateTime.UtcNow;
}