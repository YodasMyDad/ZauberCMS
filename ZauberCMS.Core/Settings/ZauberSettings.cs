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
    public EmailSettings Email { get; set; } = new();
    public PluginSettings Plugins { get; set; } = new()
    {
        EmailProvider = "ZauberCMS.Core.Providers.SmtpEmailProvider",
        StorageProvider = "ZauberCMS.Core.Providers.DiskStorageProvider"
    };

    public Identity Identity { get; set; } = new();
    public ImageResizeSettings ImageResize { get; set; } = new();
    public string? NotFoundComponent { get; set; } = "ZauberCMS.Components.Pages.NotFound404";
    public string? StarterComponent { get; set; } = "ZauberCMS.Components.Pages.NewSite";
    public string? MissingView { get; set; } = "ZauberCMS.Components.Pages.MissingView";
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
    
    public string DefaultLoginRedirectUrl { get; set; } = "/account/login";
}

public class ImageResizeSettings
{
    public bool EnableMiddleware { get; set; } = true;
    public List<string> ContentRoots { get; set; } = ["img", "images", "media"];
    public string? WebRoot { get; set; }
    public string? CacheRoot { get; set; }
    public bool AllowUpscale { get; set; } = true;
    public int DefaultQuality { get; set; } = 99;
    public int PngCompressionLevel { get; set; } = 6;
    public bool HashOriginalContent { get; set; } = false;
    public ImageCacheSettings Cache { get; set; } = new();
    public ImageResponseCacheSettings ResponseCache { get; set; } = new();
}

public class ImageCacheSettings
{
    public int FolderSharding { get; set; } = 2;
    public bool PruneOnStartup { get; set; } = false;
    public long MaxCacheBytes { get; set; } = 0;
}

public class ImageResponseCacheSettings
{
    public int ClientCacheSeconds { get; set; } = 604800;
    public bool SendETag { get; set; } = true;
    public bool SendLastModified { get; set; } = true;
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