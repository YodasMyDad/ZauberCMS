using ZauberCMS.Core.Extensions;

namespace ZauberCMS.Core.Audit.Models;

public class Audit
{
    public Guid Id { get; set; } = Guid.NewGuid().NewSequentialGuid();
    public DateTime DateCreated { get; set; } = DateTime.UtcNow;
    public DateTime DateUpdated { get; set; } = DateTime.UtcNow;
    public string? Description { get; set; }
}