using System;
using ZauberCMS.Core.Membership.Models;

namespace ZauberCMS.Core.Membership.Parameters;

public class GetRoleParameters
{
    public Guid? Id { get; set; }
    public bool AsNoTracking { get; set; } = true;
}