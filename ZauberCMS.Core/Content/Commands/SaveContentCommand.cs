using MediatR;
using ZauberCMS.Core.Shared.Models;

namespace ZauberCMS.Core.Content.Commands;

public class SaveContentCommand : IRequest<HandlerResult<List<Models.Content>>>
{
    public List<Models.Content> Content { get; set; } = [];
}