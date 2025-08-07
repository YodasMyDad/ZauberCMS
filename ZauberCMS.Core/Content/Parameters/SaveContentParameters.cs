using ZauberCMS.Core.Content.Models;
using ZauberCMS.Core.Membership.Models;
using ZauberCMS.Core.Shared.Models;

namespace ZauberCMS.Core.Content.Parameters;

public class SaveContentParameters
{
    public Content? Content { get; set; }
    public List<Role> Roles { get; set; } = [];
    public bool UpdateContentRoles { get; set; }
    public bool ExcludePropertyData { get; set; }
    
    /// <summary>
    /// This saves the content as unpublished only and leaves the original content intact
    /// </summary>
    public bool SaveUnpublishedOnly { get; set; }
}
