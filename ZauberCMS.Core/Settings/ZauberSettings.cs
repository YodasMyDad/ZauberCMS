namespace ZauberCMS.Core.Settings;

public class ZauberSettings
{
    public string? NewUserStartingRole { get; set; }
    public string? DatabaseProvider { get; set; }
    public string? UploadFolderName { get; set; }
    public List<string> AdminEmailAddresses { get; set; } = [];
    public List<string> IgnoredPaths { get; set; } = [];
    public long MaxUploadFileSizeInBytes { get; set; }
    public int MaxImageSizeInPixels { get; set; }
    public bool UseRadzen { get; set; }
    public bool EnablePathUrls { get; set; }
    public List<string> AllowedFileTypes { get; set; } = [];
    public EmailSettings Email { get; set; } = new();
    public PluginSettings Plugins { get; set; } = new();
    public Identity Identity { get; set; } = new();
    public string? NotFoundComponent { get; set; }
    public string? MissingView { get; set; }
    public Dictionary<string, string> ApiKeys { get; set; } = [];
    public List<string> AllowedAdminIpAddress { get; set; } = [];
}

public class Identity
{
    public string? AccountLayout { get; set; }
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