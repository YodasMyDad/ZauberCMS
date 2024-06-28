using ZauberCMS.Core.Extensions;

namespace ZauberCMS.Core.Membership.Models;

public class UserPropertyValue
{
    public Guid Id { get; set; } = Guid.NewGuid().NewSequentialGuid();
    public string Alias { get; set; } = string.Empty;
    public Guid ContentTypePropertyId { get; set; }
    public string Value { get; set; } = string.Empty;
    public DateTime? DateUpdated { get; set; } = DateTime.UtcNow;
    public Guid UserId { get; set; }    
    public User User { get; set; }
}