using MediatR;
using ZauberCMS.Core.Content.Models;
using ZauberCMS.Core.Shared.Models;

namespace ZauberCMS.Core.Content.Commands;

public class DeleteContentTypeCommand : IRequest<HandlerResult<ContentType>>
{
    public Guid ContentTypeId { get; set; }
}