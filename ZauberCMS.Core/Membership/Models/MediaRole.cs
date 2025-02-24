namespace ZauberCMS.Core.Membership.Models;

public class MediaRole
{
    public Media.Models.Media Media { get; set; } = null!;
    public Guid MediaId { get; set; }
    public Role Role { get; set; } = null!;
    public Guid RoleId { get; set; }
}