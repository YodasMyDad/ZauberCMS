namespace ZauberCMS.Core.Content.Models;

public class ContentPropertyData
{
    public string Alias { get; set; } = string.Empty;
    public string ComponentType { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public DateTime? DateUpdated { get; set; } = DateTime.UtcNow;
    
    // Settings? 
}