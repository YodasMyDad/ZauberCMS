using MediatR;

namespace ZauberCMS.Core.Content.Commands;

public class GetContentCommand : IRequest<Models.Content>
{
    /// <summary>
    /// Whether this is to be cached
    /// </summary>
    public bool Cached { get; set; } 
    
    public Guid? Id { get; set; }
    public bool IncludeChildren { get; set; }
    public bool IncludeParent { get; set; }
    public bool AsNoTracking { get; set; } = true;
    public string ContentTypeAlias { get; set; } = string.Empty;
}