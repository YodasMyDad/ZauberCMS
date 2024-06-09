namespace ZauberCMS.Core.Settings;

public class ZauberSettings
{
    public string? NewUserStartingRole { get; set; }
    public string? UploadFolderName { get; set; }
    public long MaxUploadFileSizeInBytes { get; set; }
    public int MaxImageSizeInPixels { get; set; }
    public List<string> AllowedFileTypes { get; set; } = [];
    public EmailSettings Email { get; set; } = new();
    public PluginSettings Plugins { get; set; } = new();
}

public class PluginSettings
{
    public string? StorageProvider { get; set; }
    public string? EmailProvider { get; set; }
}

public class EmailSettings
{
    public string? SenderEmail { get; set; }
    public SmtpSettings Smtp { get; set; } = new();
}

public class SmtpSettings
{
    public string? Host { get; set; }
    public int Port { get; set; }
    public string? Username { get; set; }
    public string? Password { get; set; }
}