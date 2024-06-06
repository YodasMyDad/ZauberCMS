using MediatR;

namespace ZauberCMS.Core.Media.Commands;

public class GetMediaCommand : IRequest<Models.Media>
{
    public Guid? Id { get; set; }
    public bool IncludeChildren { get; set; }
    public bool IncludeParent { get; set; }
    public bool AsNoTracking { get; set; } = true;
    public string ContentTypeAlias { get; set; } = string.Empty;
}