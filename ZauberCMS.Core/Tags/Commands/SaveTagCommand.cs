using MediatR;
using ZauberCMS.Core.Shared.Models;
using ZauberCMS.Core.Tags.Models;

namespace ZauberCMS.Core.Tags.Commands;

public class SaveTagCommand : IRequest<HandlerResult<Tag>>
{
    public Guid? Id { get; set; }
    public string? TagName { get; set; }
    public int SortOrder { get; set; }
}