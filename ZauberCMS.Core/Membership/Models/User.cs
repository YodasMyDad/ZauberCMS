using Microsoft.AspNetCore.Identity;

namespace ZauberCMS.Core.Membership.Models;

public class User : IdentityUser<Guid>
{
    public List<UserRole> UserRoles { get; set; } = [];

    //public Media? ProfileImage { get; set; }
    //public Guid? ProfileImageId { get; set; }
    
    public DateTime DateCreated { get; set; } = DateTime.UtcNow;
    public DateTime DateUpdated { get; set; } = DateTime.UtcNow;

    public Dictionary<string, object> ExtendedData { get; set; } = new();
}