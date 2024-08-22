using MediatR;

namespace ZauberCMS.Core.Content.Commands;

public class GetContentBySlugCommand : IRequest<Models.Content?>
{
    /// <summary>
    /// The slug to find the content
    /// </summary>
    public string? Slug { get; set; }
    
    /// <summary>
    /// Whether or not this is root content
    /// </summary>
    public bool IsRootContent { get; set; }
    
    /// <summary>
    /// Include children in this model
    /// </summary>
    public bool IncludeChildren { get; set; }
    
    /// <summary>
    /// Ignores the internal redirect
    /// </summary>
    public bool IgnoreInternalRedirect { get; set; }
    
    public string? Url { get; set; }
}