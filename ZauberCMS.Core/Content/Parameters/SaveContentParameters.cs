using System.Collections.Generic;
using ZauberCMS.Core.Membership.Models;
using ZauberCMS.Core.Shared.Models;

namespace ZauberCMS.Core.Content.Parameters;

public class SaveContentParameters
{
    public Content.Models.Content? Content { get; set; }
    public List<Role> Roles { get; set; } = [];
    public bool UpdateContentRoles { get; set; }
    public bool ExcludePropertyData { get; set; }
    public bool SaveUnpublishedOnly { get; set; }
}