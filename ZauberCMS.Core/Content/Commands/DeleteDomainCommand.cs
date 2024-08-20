using MediatR;
using ZauberCMS.Core.Content.Models;
using ZauberCMS.Core.Shared.Models;

namespace ZauberCMS.Core.Content.Commands;

public class DeleteDomainCommand : IRequest<HandlerResult<Domain?>>
{
    public Guid? Id { get; set; }
    public Guid? ContentId { get; set; }
}