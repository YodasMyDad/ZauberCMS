using MediatR;
using ZauberCMS.Core.Content.Models;
using ZauberCMS.Core.Shared.Models;

namespace ZauberCMS.Core.Content.Commands;

public class SaveDomainCommand : IRequest<HandlerResult<Domain>>
{
    public Domain? Domain { get; set; } 
}