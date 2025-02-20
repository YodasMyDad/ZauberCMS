using ZauberCMS.Core.Content.Models;
using ZauberCMS.Core.Extensions;
using ZauberCMS.Core.Shared.Interfaces;

namespace ZauberCMS.Core.Seo.Models;

public class SeoRedirect : ITreeItem
{
    /// <summary>
    /// Represents the unique identifier for the SEO redirect entity.
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid().NewSequentialGuid();

    public string? Name { get; set; }

    /// <summary>
    /// Represents the associated domain entity for the SEO redirect.
    /// This defines the context or scope of the redirect within a specific domain.
    /// </summary>
    public Domain? Domain { get; set; }

    /// <summary>
    /// Represents the unique identifier for the associated domain of the SEO redirect entity.
    /// </summary>
    public Guid? DomainId { get; set; }

    /// <summary>
    /// Represents the source URL from which the SEO redirect originates.
    /// </summary>
    public string? FromUrl { get; set; }

    /// <summary>
    /// Represents the target URL to which the SEO redirect should point.
    /// </summary>
    public string? ToUrl { get; set; }

    /// <summary>
    /// Determines whether the SEO redirect is permanent or temporary.
    /// </summary>
    public bool IsPermanent { get; set; } = true;
    
    /// <summary>
    /// The date and time when the content was created.
    /// </summary>
    public DateTime DateCreated { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets the date and time of the last update for the content.
    /// </summary>
    public DateTime DateUpdated { get; set; } = DateTime.UtcNow;
}