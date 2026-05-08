namespace ZauberCMS.Core.Settings;

public class GlobalSettings
{
    // General
    public Dictionary<string, string> ApiKeys { get; set; } = [];
    
    // Media    
    public long MaxUploadFileSizeInBytes { get; set; } = 5242880;
    public int MaxImageSizeInPixels { get; set; } = 2500;
    // Note: .svg is intentionally excluded from defaults. SVG files can carry inline
    // <script> and event handlers; serving them same-origin as /admin makes them a
    // stored-XSS vector against admins. Operators who need SVG support can opt in
    // explicitly via GlobalSettings.AllowedFileTypes.
    public List<string> AllowedFileTypes { get; set; } =
    [
        // Images
        ".jpg", ".jpeg", ".png", ".gif", ".webp", ".ico",

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