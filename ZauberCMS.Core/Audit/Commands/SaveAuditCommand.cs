using MediatR;
using ZauberCMS.Core.Shared.Models;

namespace ZauberCMS.Core.Audit.Commands;

public class SaveAuditCommand : IRequest<HandlerResult<Models.Audit>>
{
    public Models.Audit? Audit { get; set; }
}