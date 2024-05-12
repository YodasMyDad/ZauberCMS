using ZauberCMS.Core.Extensions;

namespace ZauberCMS.Core.Content.Models;

public class ContentTypeProperty
{
    public Guid Id { get; set; } = Guid.NewGuid().NewSequentialGuid();
    public string? Name { get; set; }
    public string? Alias { get; set; }
    public string? Description { get; set; }
    public string? Component { get; set; }
    public DateTime? DateUpdated { get; set; } = DateTime.UtcNow;
    public int SortOrder { get; set; }
    public string? TabAlias { get; set; }
}