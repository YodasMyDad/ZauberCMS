using ZauberCMS.Core.Extensions;
using ZauberCMS.Core.Membership.Models;

namespace ZauberCMS.Core.Audit.Models;

public class Audit
{
    public Guid Id { get; set; } = Guid.NewGuid().NewSequentialGuid();
    public DateTime DateCreated { get; set; } = DateTime.UtcNow;
    public DateTime DateUpdated { get; set; } = DateTime.UtcNow;
    public string? Description { get; set; }
    
    public Guid? UserId { get; set; }
    public User? User { get; set; }
    
    public Guid? ContentId { get; set; }
    public Content.Models.Content? Content { get; set; }
    
    public Guid? MediaId { get; set; }
    public Media.Models.Media? Media { get; set; }
}

public enum AuditAction
{
    Create,
    Update,
    Delete,
    Misc
}