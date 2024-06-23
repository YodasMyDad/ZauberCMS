using MediatR;
using ZauberCMS.Core.Membership.Models;
using ZauberCMS.Core.Shared.Models;

namespace ZauberCMS.Core.Membership.Commands;

public class DeleteUserCommand : IRequest<HandlerResult<User>>
{
    public Guid UserId { get; set; }
}