namespace ZauberCMS.Core.Settings;

public class GlobalSettings
{
    // General
    public Dictionary<string, string> ApiKeys { get; set; } = [];
    
    // Media    
    public long MaxUploadFileSizeInBytes { get; set; } = 5242880;
    public int MaxImageSizeInPixels { get; set; } = 2500;
    public List<string> AllowedFileTypes { get; set; } = 
    [
        // Images
        ".jpg", ".jpeg", ".png", ".gif", ".webp", ".svg", ".ico",
        
        // Documents (safer formats)
        ".pdf", ".txt",
        
        // Video
        ".mp4", ".webm",
        
        // Audio
        ".mp3", ".wav", ".ogg"
    ];
    
    // Identity
    public List<string> AllowedAdminIpAddress { get; set; } = [];
    public List<string> AdminEmailAddresses { get; set; } = [];
}