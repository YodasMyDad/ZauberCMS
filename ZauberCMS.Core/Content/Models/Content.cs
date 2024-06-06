using System.ComponentModel.DataAnnotations.Schema;
using ZauberCMS.Core.Content.Interfaces;
using ZauberCMS.Core.Extensions;

namespace ZauberCMS.Core.Content.Models;

public class Content : IContent
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
    public ContentType? ContentType { get; set; }
    public string? ContentTypeAlias { get; set; }
    
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
    public Guid? InternalRedirectId { get; set; }
    
    [NotMapped] // Prevents property from being mapped to a DB column
    public string? InternalRedirectIdAsString
    {
        get => InternalRedirectId.ToString();
        set
        {
            if (string.IsNullOrEmpty(value))
            {
                InternalRedirectId = Guid.Empty;
            }
            else
            {
                if (Guid.TryParse(value, out var guidValue))
                {
                    InternalRedirectId = guidValue;
                }
            }
        }
    }
    
    /// <summary>
    /// The id of the parent content node if there is one
    /// </summary>
    public Guid? ParentId { get; set; }
    public Content? Parent { get; set; }

    /// <summary>
    /// The date and time when the content was created.
    /// </summary>
    public DateTime DateCreated { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets the date and time of the last update for the content.
    /// </summary>
    public DateTime DateUpdated { get; set; } = DateTime.UtcNow;


    /// <summary>
    /// The component used to render the content
    /// </summary>
    public string ViewComponent { get; set; } = string.Empty;
    
    /// <summary>
    /// The content properties
    /// </summary>
    public List<ContentValue> ContentPropertyData { get; set; } = [];


    private Dictionary<string, ContentValue>? _contentValues;

    public Dictionary<string, ContentValue> ContentValues
    {
        get { return _contentValues ??= ContentPropertyData.ToDictionary(x => x.Alias, x => x); }
    }

    /// <summary>
    /// If parent ids are set this could have children
    /// </summary>
    public List<Content> Children { get; set; } = [];
}