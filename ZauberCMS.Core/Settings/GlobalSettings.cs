namespace ZauberCMS.Core.Settings;

public class GlobalSettings
{
    // General
    public Dictionary<string, string> ApiKeys { get; set; } = [];
    
    // Media    
    public long MaxUploadFileSizeInBytes { get; set; } = 5242880;
    public int MaxImageSizeInPixels { get; set; } = 1500;
    public List<string> AllowedFileTypes { get; set; } = [".jpg", ".jpeg", ".png", ".gif", ".svg"];
    
    // Identity
    public List<string> AllowedAdminIpAddress { get; set; } = [];
    public List<string> AdminEmailAddresses { get; set; } = [];
}