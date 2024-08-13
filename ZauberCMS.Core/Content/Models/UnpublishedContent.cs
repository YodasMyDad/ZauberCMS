using ZauberCMS.Core.Extensions;

namespace ZauberCMS.Core.Content.Models;

public class UnpublishedContent
{
    /// <summary>
    /// Id
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid().NewSequentialGuid();

    /// <summary>
    /// Json string of the unpublished content
    /// </summary>
    public Content JsonContent { get; set; } = new();
    
    /// <summary>
    /// The date and time when the content was created.
    /// </summary>
    public DateTime DateCreated { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets the date and time of the last update for the content.
    /// </summary>
    public DateTime DateUpdated { get; set; } = DateTime.UtcNow;
}