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
    /// Outside Development, a request that throws is shown this page with status 500 (ZA-098, Roadmap D33): a
    /// content page at that address (create one at <c>/error</c> to give the site its own error page) or a page of
    /// the site's own. When there is none, or it fails as well, a plain built-in page is sent. Empty adds no
    /// handler, for a site that sets up its own <c>UseExceptionHandler</c>.
    /// </summary>
    public string? ErrorPagePath { get; set; } = "/error";

    /// <summary>
    /// How many versions of each page are kept. The daily job removes older ones, never the published version or
    /// the latest draft. 0 keeps every version (the table then grows with every save).
    /// </summary>
    public int ContentVersionsToKeep { get; set; } = 50;

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
    public FormProtectionSettings FormProtection { get; set; } = new();
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

    /// <summary>
    /// Recovery for a site that has no administrator left: the email address or user name of an existing user
    /// who is made an administrator the next time the site starts (a locked one is unlocked; one deleted before
    /// deleting erased users is restored, an erased one cannot be). Remove the key once it has done its job; while
    /// it is set, every start logs a warning.
    /// </summary>
    public string? RecoverAdminUser { get; set; }
    public string? AccountLayout { get; set; } = "ZauberCMS.Components.Pages.BlankLayout";
    
    public string DefaultLoginRedirectUrl { get; set; } = "/account/login";

    /// <summary>
    /// Requests each visitor address may make to the sign-in pages (login, forgot password, external sign-in) in the
    /// window, refused ones included (ZA-149). Behind a proxy, set <c>Zauber:ForwardedHeaders</c> or every visitor
    /// shares one allowance.
    /// </summary>
    public RateLimitSettings LoginRateLimit { get; set; } = new() { PermitLimit = 10, WindowMinutes = 5 };

    /// <summary>Registration forms each visitor address may send in the window (looking at the form is free).</summary>
    public RateLimitSettings RegisterRateLimit { get; set; } = new() { PermitLimit = 5, WindowMinutes = 10 };
}

/// <summary>A sliding-window limit per visitor address (ZA-149).</summary>
public class RateLimitSettings
{
    /// <summary>How many requests the window allows. 0 turns the limit off (not for a public site).</summary>
    public int PermitLimit { get; set; }

    /// <summary>The length of the window in minutes.</summary>
    public int WindowMinutes { get; set; }

    /// <summary>The limiter options: the window in five segments, no queueing.</summary>
    public System.Threading.RateLimiting.SlidingWindowRateLimiterOptions ToLimiterOptions() => new()
    {
        PermitLimit = PermitLimit,
        Window = TimeSpan.FromMinutes(Math.Max(1, WindowMinutes)),
        SegmentsPerWindow = 5,
        QueueProcessingOrder = System.Threading.RateLimiting.QueueProcessingOrder.OldestFirst,
        QueueLimit = 0
    };
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

/// <summary>For <c>IFormProtectionService</c>: the registration page, and any form of a site that uses it.</summary>
public class FormProtectionSettings
{
    /// <summary>
    /// Seconds that must pass between showing a form and receiving it. A form that comes back faster was
    /// filled in by a script. 0 turns the check off.
    /// </summary>
    public int MinimumFillSeconds { get; set; } = 3;
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
    /// <summary>
    /// Whether a resize may make an image bigger than its original. Off by default (ZA-104, Roadmap D36): a request
    /// for a larger size returns the original size, so visitors cannot make the site build huge copies.
    /// </summary>
    public bool AllowUpscale { get; set; }
    public int DefaultQuality { get; set; } = 99;
    public int PngCompressionLevel { get; set; } = 6;
    public bool HashOriginalContent { get; set; }
    public ImageCacheSettings Cache { get; set; } = new();
    public ImageResponseCacheSettings ResponseCache { get; set; } = new();
}

public class ImageCacheSettings
{
    public int FolderSharding { get; set; } = 2;
    public bool PruneOnStartup { get; set; }
    /// <summary>
    /// The most disk space resized copies may take, in bytes; the oldest are removed when a new copy goes over it.
    /// 2 GB by default (ZA-104, Roadmap D36), so requests for many sizes cannot fill the disk. 0 is no limit.
    /// </summary>
    public long MaxCacheBytes { get; set; } = DefaultMaxCacheBytes;

    /// <summary>The default for <see cref="MaxCacheBytes"/>: 2 GB.</summary>
    public const long DefaultMaxCacheBytes = 2L * 1024 * 1024 * 1024;
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

    /// <summary>
    /// Full type name of the <c>ICaptchaProvider</c> the registration page uses. When empty, the provider the
    /// site's project contains is used (the first by type name when there are several); with none, no CAPTCHA.
    /// </summary>
    public string? CaptchaProvider { get; set; }
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