using MediatR;
using ZauberCMS.Core.Shared.Models;

namespace ZauberCMS.Core.Content.Commands;

public class DeleteContentCommand : IRequest<HandlerResult<Models.Content>>
{
    public Guid ContentId { get; set; }
}