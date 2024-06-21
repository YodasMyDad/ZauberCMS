using MediatR;
using ZauberCMS.Core.Membership.Models;

namespace ZauberCMS.Core.Membership.Commands;

public class GetRoleCommand : IRequest<Role>
{
    public Guid? Id { get; set; }
    public bool AsNoTracking { get; set; } = true;
}