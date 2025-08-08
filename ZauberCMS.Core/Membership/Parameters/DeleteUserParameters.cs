using System;
using ZauberCMS.Core.Membership.Models;
using ZauberCMS.Core.Shared.Models;

namespace ZauberCMS.Core.Membership.Parameters;

public class DeleteUserParameters
{
    public Guid UserId { get; set; }
    public Guid Id { get; set; }
}