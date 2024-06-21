using MediatR;
using ZauberCMS.Core.Membership.Models;
using ZauberCMS.Core.Shared.Models;

namespace ZauberCMS.Core.Membership.Commands;

public class SaveRoleCommand : IRequest<HandlerResult<Role>>
{
    public Role? Role { get; set; }
}