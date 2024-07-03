using ZauberCMS.Core.Extensions;

namespace ZauberCMS.Core.Media.Models;

public class Media
{
    /// <summary>
    /// Represents the unique identifier (ID) property of a Media object.
    /// </summary>
    /// <value>
    /// The ID of the media.
    /// </value>
    /// <remarks>
    /// The ID property is used to uniquely identify a Media object.
    /// </remarks>
    public Guid Id { get; set; } = Guid.NewGuid().NewSequentialGuid();
    
    /// <summary>
    /// Represents the URL property of a Media object.
    /// </summary>
    /// <value>
    /// The URL of the media.
    /// </value>
    /// <remarks>
    /// The URL property is used to store the location of the media file.
    /// </remarks>
    public string? Url { get; set; }
    
    /// <summary>
    /// Name of the media item
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Represents the alternate text (AltTag) property of a Media object.
    /// </summary>
    /// <value>
    /// The alternate text for the media.
    /// </value>
    /// <remarks>
    /// The AltTag property is used to provide alternate text for media objects such as images. It describes the content of the media in a textual format, which can be useful for accessibility purposes or when the media cannot be displayed.
    /// </remarks>
    public string? AltTag { get; set; }

    /// <summary>
    /// Represents the type of media.
    /// </summary>
    /// <remarks>
    /// The MediaType enum is used to categorize different types of media.
    /// </remarks>
    public MediaType MediaType { get; set; }
    
    /// <summary>
    /// The size of the file in bytes
    /// </summary>
    public long FileSize { get; set; }

    /// <summary>
    /// Represents the width property of an image
    /// </summary>
    /// <remarks>
    /// Only used for Images
    /// </remarks>
    public long Width { get; set; }

    /// <summary>
    /// Represents the height property of an image
    /// </summary>
    /// <remarks>
    /// Only used for Images
    /// </remarks>
    public long Height { get; set; }
    
    /// <summary>
    /// The id of the parent media node if there is one
    /// </summary>
    public Guid? ParentId { get; set; }
    public Media? Parent { get; set; }
    
    /// <summary>
    /// Date the file is created.
    /// </summary>
    public DateTime DateCreated { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// Date the file is updated.
    /// </summary>
    public DateTime DateUpdated { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Extended data saved on this file
    /// </summary>
    public Dictionary<string, object> ExtendedData { get; set; } = new();
    
    /// <summary>
    /// If parent ids are set this could have children
    /// </summary>
    public List<Media> Children { get; set; } = [];
}