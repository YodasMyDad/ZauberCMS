using MediatR;

namespace ZauberCMS.Core.Content.Commands;

public class GetContentCommand : IRequest<Models.Content>
{
    public Guid? Id { get; set; }
    public bool IncludeChildren { get; set; }
    public bool IncludeParent { get; set; }

    public string ContentTypeAlias { get; set; } = string.Empty;
}