using ZauberCMS.Core.Extensions;

namespace ZauberCMS.Core.Tags.Models;

public class TagItem
{
    public Guid Id { get; set; } = Guid.NewGuid().NewSequentialGuid();
    public DateTime DateCreated { get; set; } = DateTime.UtcNow;
    public DateTime DateUpdated { get; set; } = DateTime.UtcNow;
    public Guid TagId { get; set; }
    public Tag? Tag { get; set; }
    public Guid ItemId { get; set; }
    public Content.Models.Content? Content { get; set; }
}