using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;
using ZauberCMS.Core.Extensions;
using ZauberCMS.Core.Membership.Models;
using ZauberCMS.Core.Shared.Interfaces;
using ZauberCMS.Core.Shared.Models;

namespace ZauberCMS.Core.Content.Models;

public class ContentType : ITreeItem
{
    /// <summary>
    /// Gets or sets the unique identifier of the content type.
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid().NewSequentialGuid();

    /// <summary>
    /// Gets or sets the name of the content type.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Gets or sets the description of the content type.
    /// </summary>
    public string? Description { get; set; }
    
    /// <summary>
    /// Gets or sets the alias of the content type.
    /// </summary>
    public string? Alias { get; set; }
    
    /// <summary>
    /// Gets or sets the icon for the content type
    /// </summary>
    public string? Icon { get; set; }

    /// <summary>
    /// Whether this content is used in BlockList or another modular way
    /// </summary>
    public bool IsElementType { get; set; }
    
    /// <summary>
    /// Is this content type allowed at the tree route
    /// </summary>
    public bool AllowAtRoot { get; set; }
    
    /// <summary>
    /// Enables the list view of child content instead of them showing as child items in the tree
    /// </summary>
    public bool EnableListView { get; set; }
    
    /// <summary>
    /// A flag to return children when this page is rendered to save multiple content queries
    /// </summary>
    public bool IncludeChildren { get; set; }
    
    /// <summary>
    /// The id of the last person to update the content type 
    /// </summary>
    public Guid? LastUpdatedById { get; set; }
    public User? LastUpdatedBy { get; set; }
    
    /// <summary>
    /// Gets or sets the date and time when the entity was created.
    /// </summary>
    public DateTime? DateCreated { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets the date and time when the entity was last updated.
    /// </summary>
    public DateTime? DateUpdated { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// The properties available on this ContentType
    /// </summary>
    public List<PropertyType> ContentProperties { get; set; } = [];

    /// <summary>
    /// The available Views for this ContentType that the user can select to display their content
    /// </summary>
    public List<string> AvailableContentViews { get; set; } = [];
    
    /// <summary>
    /// List of optionally allowed child content types
    /// </summary>
    public List<Guid> AllowedChildContentTypes { get; set; } = [];

    /// <summary>
    /// Gets or sets the list of tabs associated with the content type.
    /// </summary>
    /// <remarks>
    /// Tabs are used to organize the properties of a content type into separate sections.
    /// Each tab represents a logical grouping of properties.
    /// </remarks>
    public List<Tab> Tabs { get; set; } = [new() {Id = Constants.Guids.ContentTypeSystemTabId, IsSystemTab = true, SortOrder = 100, Name = "System"}];
    
    // ReSharper disable once CollectionNeverUpdated.Global
    /// <summary>
    /// The Content items using this ContentType
    /// </summary>
    [JsonIgnore]
    public List<Content> LinkedContent { get; set; } = [];
    
    /// <summary>
    /// Parent element ID
    /// </summary>
    public Guid? ParentId { get; set; }
    
    /// <summary>
    /// Optional Image 
    /// </summary>
    public Guid? MediaId { get; set; }
    
    [NotMapped] // Prevents property from being mapped to a DB column
    public string? MediaIdAsString
    {
        get => MediaId.ToString();
        set
        {
            if (string.IsNullOrEmpty(value))
            {
                MediaId = Guid.Empty;
            }
            else
            {
                if (Guid.TryParse(value, out var guidValue))
                {
                    MediaId = guidValue;
                }
            }
        }
    }
}