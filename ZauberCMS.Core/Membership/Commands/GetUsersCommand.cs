using MediatR;
using ZauberCMS.Core.Membership.Models;

namespace ZauberCMS.Core.Membership.Commands;

public class GetUsersCommand : IRequest<List<User>>
{
    public List<Guid> Ids { get; set; } = new();
}