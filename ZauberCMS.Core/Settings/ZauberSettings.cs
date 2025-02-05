namespace ZauberCMS.Core.Settings;

public class ZauberSettings
{
    public string? NewUserStartingRole { get; set; } = "Member";
    public string? DatabaseProvider { get; set; } = "Sqlite";
    public string? ConnectionString { get; set; } = "DataSource=app.db;Cache=Shared";
    public string? RedisConnectionString { get; set; }
    public string? UploadFolderName { get; set; } = "media";
    public string AdminDefaultLanguage { get; set; } = "en-US";
    public bool EnablePathUrls { get; set; }
    public bool ShowDetailedErrors { get; set; }
    public string? Default404Url { get; set; }
    public List<string> DllExclusions { get; set; } = [];
    public EmailSettings Email { get; set; } = new();
    public PluginSettings Plugins { get; set; } = new()
    {
        EmailProvider = "ZauberCMS.Core.Providers.SmtpEmailProvider",
        StorageProvider = "ZauberCMS.Core.Providers.DiskStorageProvider"
    };

    public Identity Identity { get; set; } = new();
}

public class Identity
{
    public bool PasswordRequireDigit { get; set; } = true;
    public bool PasswordRequireLowercase { get; set; } = true;
    public bool PasswordRequireNonAlphanumeric { get; set; }
    public bool PasswordRequireUppercase { get; set; } = true;
    public int PasswordRequiredLength { get; set; } = 8;
    public int PasswordRequiredUniqueChars { get; set; } = 1;
    public bool SignInRequireConfirmedAccount { get; set; }
    public string? AccountLayout { get; set; } = "ZauberCMS.Components.Pages.BlankLayout";
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