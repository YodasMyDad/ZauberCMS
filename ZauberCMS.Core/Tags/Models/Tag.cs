using ZauberCMS.Core.Extensions;

namespace ZauberCMS.Core.Tags.Models;

public class Tag
{
    public Guid Id { get; set; } = Guid.NewGuid().NewSequentialGuid();
    public string TagName { get; set; } = string.Empty;
    public DateTime DateCreated { get; set; } = DateTime.UtcNow;
    public DateTime DateUpdated { get; set; } = DateTime.UtcNow;
    public int SortOrder { get; set; }
    
    public List<TagItem> TagItems { get; set; } = [];
    
    // Not mapped, used for querying
    public int Count { get; set; }
}