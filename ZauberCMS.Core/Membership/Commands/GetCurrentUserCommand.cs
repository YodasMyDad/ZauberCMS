using MediatR;
using ZauberCMS.Core.Membership.Models;

namespace ZauberCMS.Core.Membership.Commands;

public class GetCurrentUserCommand : IRequest<User?>
{
    
}