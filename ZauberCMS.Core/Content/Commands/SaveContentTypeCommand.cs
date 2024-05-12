using MediatR;
using ZauberCMS.Core.Content.Models;
using ZauberCMS.Core.Shared.Models;

namespace ZauberCMS.Core.Content.Commands;

public class SaveContentTypeCommand : IRequest<HandlerResult<ContentType>>
{
    public ContentType? ContentType { get; set; }
}