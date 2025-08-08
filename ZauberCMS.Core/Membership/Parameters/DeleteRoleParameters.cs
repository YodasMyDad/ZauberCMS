using System;
using ZauberCMS.Core.Membership.Models;
using ZauberCMS.Core.Shared.Models;

namespace ZauberCMS.Core.Membership.Parameters;

public class DeleteRoleParameters
{
    public Guid RoleId { get; set; }
    public Guid Id { get; set; }
}