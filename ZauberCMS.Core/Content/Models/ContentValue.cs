namespace ZauberCMS.Core.Content.Models;

public class ContentValue
{
    public string Alias { get; set; } = string.Empty;
    public Guid ContentTypePropertyId { get; set; }
    public string Value { get; set; } = string.Empty;
    public string Settings { get; set; } = string.Empty;
    public DateTime? DateUpdated { get; set; } = DateTime.UtcNow;
}