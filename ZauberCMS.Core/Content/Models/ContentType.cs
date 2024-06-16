using ZauberCMS.Core.Extensions;
using ZauberCMS.Core.Shared.Models;

namespace ZauberCMS.Core.Content.Models;

public class ContentType
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
    /// Gets or sets the list of tabs associated with the content type.
    /// </summary>
    /// <remarks>
    /// Tabs are used to organize the properties of a content type into separate sections.
    /// Each tab represents a logical grouping of properties.
    /// </remarks>
    public List<Tab> Tabs { get; set; } = [];
    
    // ReSharper disable once CollectionNeverUpdated.Global
    /// <summary>
    /// The Content items using this ContentType
    /// </summary>
    public List<Content> LinkedContent { get; set; } = [];
}