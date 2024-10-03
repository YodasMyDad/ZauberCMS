using MediatR;
using ZauberCMS.Core.Membership.Models;

namespace ZauberCMS.Core.Membership.Commands;

public class GetUserCommand : IRequest<User?>
{
    public bool Cached { get; set; }
    public Guid Id { get; set; }
}