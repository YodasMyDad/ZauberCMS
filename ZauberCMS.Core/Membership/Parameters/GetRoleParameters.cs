using System;
using ZauberCMS.Core.Membership.Models;

namespace ZauberCMS.Core.Membership.Parameters;

public class GetRoleParameters
{
    public Guid? Id { get; set; }
    public bool AsNoTracking { get; set; } = true;

    /// <summary>Also load the role's members (<c>Role.UserRoles</c> with each <c>User</c>, recycled users
    /// included). Off by default: a role on a site with many members loaded all of them (ZA-056).</summary>
    public bool IncludeUsers { get; set; }
}