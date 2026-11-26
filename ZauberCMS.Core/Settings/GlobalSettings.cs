namespace ZauberCMS.Core.Settings;

public class GlobalSettings
{
    // General
    public Dictionary<string, string> ApiKeys { get; set; } = [];

    /// <summary>The name of the site, asked for by the installer. Views and emails may show it.</summary>
    public string? SiteName { get; set; }

    /// <summary>
    /// The culture (for example <c>en-GB</c>) of pages that have no language of their own, on themselves, an
    /// ancestor or their domain. When empty those pages use <c>Zauber:AdminDefaultLanguage</c>, as they did before
    /// this setting existed.
    /// </summary>
    public string? DefaultLanguage { get; set; }
    
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

    /// <summary>
    /// Whether visitors can create an account: the registration page, and a first sign-in through an external
    /// provider. Off means the page answers 404 and the link on the login page is hidden. A site that existed
    /// before this setting keeps registration on; the installer asks, and suggests off.
    /// </summary>
    public bool AllowRegistration { get; set; } = true;
}