using Microsoft.AspNetCore.Identity;

namespace ZauberCMS.Core.Membership.Models;

public class UserRole : IdentityUserRole<Guid>
{
    public Role Role { get; set; } = null!;
    public User User { get; set; } = null!;
}