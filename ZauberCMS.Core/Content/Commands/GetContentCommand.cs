using MediatR;

namespace ZauberCMS.Core.Content.Commands;

public class GetContentCommand : IRequest<Models.Content?>
{
    /// <summary>
    /// Whether this is to be cached
    /// </summary>
    public bool Cached { get; set; } 
    
    /// <summary>
    /// Include any unpublished content with this query
    /// </summary>
    public bool IncludeUnpublishedContent { get; set; }

    /// <summary>
    /// Indicates whether content-related roles should be included.
    /// </summary>
    public bool IncludeContentRoles { get; set; }
    
    /// <summary>
    /// The id of the specific content
    /// </summary>
    public Guid? Id { get; set; }
    
    /// <summary>
    /// Include the child content in the return data
    /// </summary>
    public bool IncludeChildren { get; set; }
    
    /// <summary>
    /// Include the parent in the returned data
    /// </summary>
    public bool IncludeParent { get; set; }
    
    /// <summary>
    /// Where or not to include unpublished content in this query
    /// </summary>
    public bool IncludeUnpublished { get; set; }
    
    /// <summary>
    /// Whether this is a EF core tracked query or not
    /// </summary>
    public bool AsNoTracking { get; set; } = true;
    
    /// <summary>
    /// The content type alias
    /// </summary>
    public string ContentTypeAlias { get; set; } = string.Empty;
}