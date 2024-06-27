using Microsoft.AspNetCore.Identity;
using ZauberCMS.Core.Content.Models;

namespace ZauberCMS.Core.Membership.Models;

public class User : IdentityUser<Guid>
{
    public List<UserRole> UserRoles { get; set; } = [];

    //public Media? ProfileImage { get; set; }
    //public Guid? ProfileImageId { get; set; }
    
    public DateTime DateCreated { get; set; } = DateTime.UtcNow;
    public DateTime DateUpdated { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// The content properties
    /// </summary>
    public List<PropertyValue> PropertyData { get; set; } = [];

    private Dictionary<string, PropertyValue>? _contentValues;

    public Dictionary<string, PropertyValue> ContentValues
    {
        get { return _contentValues ??= PropertyData.ToDictionary(x => x.Alias, x => x); }
    }
    
    public Dictionary<string, object> ExtendedData { get; set; } = new();
}