using System;
using ZauberCMS.Core.Membership.Models;

namespace ZauberCMS.Core.Membership.Parameters;

public class GetUserParameters
{
    public bool Cached { get; set; }
    public Guid Id { get; set; }
}