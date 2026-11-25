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

    /// <summary>
    /// The public address of the site, for example <c>https://www.example.com</c>. Links in emails (password
    /// reset, email confirmation) are built from it. When it is empty they are built from the Host header of the
    /// request, which the sender of the request controls, so set this (or restrict <c>AllowedHosts</c>) on any
    /// site that sends email. The SEO checker may also fetch this host when it is on a private address.
    /// </summary>
    public string? SiteUrl { get; set; }
    public EmailSettings Email { get; set; } = new();
    public PluginSettings Plugins { get; set; } = new()
    {
        EmailProvider = "ZauberCMS.Core.Providers.SmtpEmailProvider",
        StorageProvider = "ZauberCMS.Core.Providers.DiskStorageProvider"
    };

    public Identity Identity { get; set; } = new();
    public ImageResizeSettings ImageResize { get; set; } = new();
    public ForwardedHeadersSettings ForwardedHeaders { get; set; } = new();
    public HtmlSanitizerSettings HtmlSanitizer { get; set; } = new();
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

/// <summary>
/// For a site behind a reverse proxy or load balancer. Without it the application sees the proxy's address as
/// the visitor's: the login rate limiter then counts all visitors as one, and the admin IP allow-list compares
/// the proxy's address.
/// </summary>
public class ForwardedHeadersSettings
{
    /// <summary>Read the visitor's address and scheme from X-Forwarded-For / X-Forwarded-Proto.</summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// IP addresses of the proxies whose headers are trusted. A proxy on the same machine (loopback) is always
    /// trusted. Headers from any other sender are ignored, so list every proxy that is on another machine.
    /// </summary>
    public string[]? KnownProxies { get; set; }

    /// <summary>Networks of trusted proxies in CIDR form, for example <c>10.0.0.0/8</c>.</summary>
    public string[]? KnownNetworks { get; set; }
}

public class HtmlSanitizerSettings
{
    /// <summary>
    /// Hosts an <c>&lt;iframe&gt;</c> in rich text may load from (https only). When the key is missing the defaults
    /// are YouTube and Vimeo (<c>DefaultHtmlSanitizerService.DefaultIframeHosts</c>).
    /// </summary>
    public string[]? AllowedIframeHosts { get; set; }
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