namespace ZauberCMS.Core.Content.Models;

public class ContentTypeProperty
{
    public string ComponentType { get; set; } = string.Empty;
    public DateTime? DateUpdated { get; set; } = DateTime.UtcNow;
    public int SortOrder { get; set; }
}