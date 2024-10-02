using MediatR;
using ZauberCMS.Core.Shared.Models;
using ZauberCMS.Core.Tags.Models;

namespace ZauberCMS.Core.Tags.Commands;

public class SaveTagItemCommand : IRequest<HandlerResult<TagItem>>
{
    public Guid TagId { get; set; }
    public Guid ItemId { get; set; }
}