using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Identity;
using ZauberCMS.Core.Shared.Interfaces;

namespace ZauberCMS.Core.Membership.Models;

public class User : IdentityUser<Guid>, ITreeItem
{
    public List<UserRole> UserRoles { get; set; } = [];

    //public Media? ProfileImage { get; set; }
    //public Guid? ProfileImageId { get; set; }
    
    public DateTime DateCreated { get; set; } = DateTime.UtcNow;
    public DateTime DateUpdated { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// The content properties
    /// </summary>n
    public List<UserPropertyValue> PropertyData { get; set; } = [];


    private Dictionary<string, UserPropertyValue>? _contentValues;

    public Dictionary<string, UserPropertyValue> ContentValues()
    {
        return _contentValues ??= PropertyData.ToDictionary(x => x.Alias, x => x);
    }
    
    public Dictionary<string, object> ExtendedData { get; set; } = new();
    
    /// <summary>
    /// If parent ids are set this could have children
    /// </summary>
    [JsonIgnore]
    public List<Audit.Models.Audit> Audits { get; set; } = [];
    
    public string? Name
    {
        get => this.UserName;
        set => this.UserName = value;
    }
}