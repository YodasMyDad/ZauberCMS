using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;
using ZauberCMS.Core.Content.Interfaces;
using ZauberCMS.Core.Extensions;
using ZauberCMS.Core.Languages.Models;
using ZauberCMS.Core.Membership.Models;
using ZauberCMS.Core.Tags.Models;

namespace ZauberCMS.Core.Content.Models;

public class Content : IContent<ContentPropertyValue>
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
    /// The id of the last person to update the content 
    /// </summary>
    public Guid? LastUpdatedById { get; set; }
    
    /// <summary>
    /// Last updated by User object
    /// </summary>
    public User? LastUpdatedBy { get; set; }
    
    /// <summary>
    /// Id of any unpublished content
    /// </summary>
    public Guid? UnpublishedContentId { get; set; }
    
    /// <summary>
    /// The unpublished content object
    /// </summary>
    [JsonIgnore]
    public UnpublishedContent? UnpublishedContent { get; set; }
    
    /// <summary>
    /// The path for this content in the content tree
    /// </summary>
    public List<Guid> Path { get; set; } = [];

    /// <summary>
    /// The sort order
    /// </summary>
    public int SortOrder { get; set; }

    /// <summary>
    /// Is this content allowed in the root of the tree
    /// </summary>
    public bool IsRootContent { get; set; }
    
    /// <summary>
    /// Whether this content item is published or not
    /// </summary>
    public bool Published { get; set; }
    
    /// <summary>
    /// Whether this content is set as deleted
    /// </summary>
    public bool Deleted { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the content should be hidden from navigation.
    /// </summary>
    public bool HideFromNavigation { get; set; }
    
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
    /// Manually set language of the page
    /// </summary>
    public Guid? LanguageId { get; set; }
    
    /// <summary>
    /// Manually set Language of the page
    /// </summary>
    public Language? Language { get; set; }
    
    /// <summary>
    /// The content properties
    /// </summary>
    public List<ContentPropertyValue> PropertyData { get; set; } = [];


    private Dictionary<string, ContentPropertyValue>? _contentValues;

    public Dictionary<string, ContentPropertyValue> ContentValues()
    {
        return _contentValues ??= PropertyData.ToDictionary(x => x.Alias, x => x);
    }

    /// <summary>
    /// If parent ids are set this could have children
    /// </summary>
    [JsonIgnore]
    public List<Content> Children { get; set; } = [];
    
    /// <summary>
    /// If parent ids are set this could have children
    /// </summary>
    [JsonIgnore]
    public List<Audit.Models.Audit> Audits { get; set; } = [];
    
    /// <summary>
    /// If parent ids are set this could have children
    /// </summary>
    [JsonIgnore]
    public List<TagItem> TagItems { get; set; } = [];
}