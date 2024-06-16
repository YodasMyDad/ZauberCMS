using Microsoft.AspNetCore.Identity;
using ZauberCMS.Core.Content.Models;
using ZauberCMS.Core.Shared.Models;

namespace ZauberCMS.Core.Membership.Models;

public class Role : IdentityRole<Guid>
{
    public string? Description { get; set; }
    public DateTime CreateDate { get; set; } = DateTime.UtcNow;
    public Dictionary<string, object> ExtendedData { get; set; } = new();
    /// <summary>
    /// The properties available on this Role
    /// </summary>
    public List<PropertyType> RoleProperties { get; set; } = [];
    public List<UserRole> UserRoles { get; set; } = new();
}