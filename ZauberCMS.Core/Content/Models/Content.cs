using ZauberCMS.Core.Extensions;

namespace ZauberCMS.Core.Content.Models;

public class Content
{
    public Guid Id { get; set; } = Guid.NewGuid().NewSequentialGuid();
    
    /// <summary>
    /// The name of the content
    /// </summary>
    public string? Name { get; set; }
    
    /// <summary>
    /// The Url for the content
    /// </summary>
    public string? Url { get; set; }
    
    /// <summary>
    /// ContentType ID for this content
    /// </summary>
    public Guid ContentTypeId { get; set; }
    public ContentType ContentType { get; set; } = default!;
    
    /// <summary>
    /// The sort order
    /// </summary>
    public int SortOrder { get; set; }
    
    /// <summary>
    /// Is this content allowed in the root of the tree
    /// </summary>
    public bool IsRootContent { get; set; }
    
    /// <summary>
    /// Redirects behind the scenes to another content node
    /// </summary>
    public Guid InternalRedirectId { get; set; }
    
    /// <summary>
    /// The id of the parent content node if there is one
    /// </summary>
    public Guid? ParentId { get; set; }

    /// <summary>
    /// The date and time when the content was created.
    /// </summary>
    public DateTime DateCreated { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets the date and time of the last update for the content.
    /// </summary>
    public DateTime DateUpdated { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// The content properties
    /// </summary>
    public List<ContentValue> ContentPropertyData { get; set; } = [];
}