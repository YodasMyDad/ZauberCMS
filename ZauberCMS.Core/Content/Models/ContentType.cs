using ZauberCMS.Core.Extensions;

namespace ZauberCMS.Core.Content.Models;

public class ContentType
{
    public Guid Id { get; set; } = Guid.NewGuid().NewSequentialGuid();
    public string? Name { get; set; }
    public string? Alias { get; set; }
    public DateTime? DateCreated { get; set; } = DateTime.UtcNow;
    public DateTime? DateUpdated { get; set; } = DateTime.UtcNow;
    public List<ContentTypeProperty> ContentProperties { get; set; } = [];
    
    // ReSharper disable once CollectionNeverUpdated.Global
    public List<Content> LinkedContent { get; set; } = [];
}