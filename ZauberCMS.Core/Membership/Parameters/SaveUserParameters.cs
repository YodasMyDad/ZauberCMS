using System.Collections.Generic;
using ZauberCMS.Core.Membership.Models;
using ZauberCMS.Core.Shared.Models;

namespace ZauberCMS.Core.Membership.Parameters;

public class SaveUserParameters
{
    public User? User { get; set; }
    public string? Password { get; set; }
    public List<string>? Roles { get; set; }
}