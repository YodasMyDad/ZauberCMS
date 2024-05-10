namespace ZauberCMS.Core.Content.Models;

public class ContentTypeProperty
{
    public string Name { get;set;} = string.Empty;
    public string Description { get;set;} = string.Empty;
    public string Alias { get; set; } = string.Empty;
    public string ComponentType { get; set; } = string.Empty;
    public DateTime? DateUpdated { get; set; } = DateTime.UtcNow;
    public int SortOrder { get; set; }
}