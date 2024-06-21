namespace ZauberCMS.Core.Content.Models;

public class PropertyValue
{
    public string Alias { get; set; } = string.Empty;
    public Guid ContentTypePropertyId { get; set; }
    public string Value { get; set; } = string.Empty;
    public DateTime? DateUpdated { get; set; } = DateTime.UtcNow;
}