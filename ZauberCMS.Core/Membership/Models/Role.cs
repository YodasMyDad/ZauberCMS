using Microsoft.AspNetCore.Identity;

namespace ZauberCMS.Core.Membership.Models;

public class Role : IdentityRole<Guid>
{
    public string? Description { get; set; }
    public DateTime CreateDate { get; set; } = DateTime.UtcNow;
    public Dictionary<string, object> ExtendedData { get; set; } = new();
    public List<UserRole> UserRoles { get; set; } = new();
}