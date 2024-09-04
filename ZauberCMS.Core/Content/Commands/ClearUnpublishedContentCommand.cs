using MediatR;
using ZauberCMS.Core.Content.Models;
using ZauberCMS.Core.Shared.Models;

namespace ZauberCMS.Core.Content.Commands;

public class ClearUnpublishedContentCommand : IRequest<HandlerResult<UnpublishedContent>>
{
    public Guid ContentId { get; set; }
}