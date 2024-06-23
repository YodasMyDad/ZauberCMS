using MediatR;
using ZauberCMS.Core.Membership.Models;
using ZauberCMS.Core.Shared.Models;

namespace ZauberCMS.Core.Membership.Commands;

public class DeleteRoleCommand: IRequest<HandlerResult<Role>>
{
    public Guid RoleId { get; set; }
}