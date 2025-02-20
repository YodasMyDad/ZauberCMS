using MediatR;
using ZauberCMS.Core.Shared.Models;

namespace ZauberCMS.Core.Content.Commands;

public class CopyContentCommand : IRequest<HandlerResult<Models.Content>>
{
    public Guid ContentToCopy { get; set; }
    public bool IncludeDescendants { get; set; }
    public Guid? CopyTo { get; set; }
}