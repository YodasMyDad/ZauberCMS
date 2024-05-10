using ZauberCMS.Core.Extensions;

namespace ZauberCMS.Core.Content.Models;

public class Content
{
    public Guid Id { get; set; } = Guid.NewGuid().NewSequentialGuid();
    public string? Name { get; set; }
    public DateTime? DateCreated { get; set; } = DateTime.UtcNow;
    public DateTime? DateUpdated { get; set; } = DateTime.UtcNow;
    public List<ContentPropertyData> ContentPropertyData { get; set; } = [];
}