using ImageResize.Core.Configuration;
using Microsoft.AspNetCore.Components.Endpoints;
using Microsoft.AspNetCore.Components.Server;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using System.Threading.RateLimiting;

namespace ZauberCMS.Core.Settings;

// The options of other libraries that ZauberCMS sets from Zauber settings, read from IOptions<ZauberSettings> when
// those options are first built, so settings changed in code (Configure<ZauberSettings>, PostConfigure) count as
// much as configuration does (ZA-192, Roadmap D81). Umbraco does the same with IConfigureOptions classes over its
// settings (ConfigureBackOfficeIdentityOptions, ConfigureImageSharpMiddlewareOptions). They run where the lambdas
// they replace ran, so a site's own Configure<T> keeps its old effect (ZA-232): registered after AddZauberCms it runs
// after them and wins (IdentityOptions, ImageResizeOptions, RedisCacheOptions); the "login" and "register" policies
// are added once, so a site that adds a policy of either name gets RateLimiterOptions' duplicate-name error, as
// before; detailed errors are a post-configure step that only turns them on.

/// <summary><c>Zauber:ImageResize</c> for the image resizer.</summary>
internal sealed class ImageResizeOptionsFromZauberSettings(IOptions<ZauberSettings> settings,
    IWebHostEnvironment environment) : IConfigureOptions<ImageResizeOptions>
{
    public void Configure(ImageResizeOptions options)
    {
        ZauberSettings zauberSettings;
        try
        {
            zauberSettings = settings.Value;
        }
        catch (OptionsValidationException)
        {
            // ImageResize validates its options when the host starts, as the Zauber settings are: when these fail
            // their own check, that failure stops the start naming the setting (D48), not a second copy of it here
            return;
        }

        var imageResize = zauberSettings.ImageResize;
        options.EnableMiddleware = imageResize.EnableMiddleware;
        options.ContentRoots = [.. imageResize.ContentRoots];
        options.WebRoot = imageResize.WebRoot ?? environment.WebRootPath;
        options.CacheRoot = imageResize.CacheRoot ?? Path.Combine(environment.WebRootPath, "_mediacache");
        options.AllowUpscale = imageResize.AllowUpscale;
        options.DefaultQuality = imageResize.DefaultQuality;
        options.PngCompressionLevel = imageResize.PngCompressionLevel;
        options.HashOriginalContent = imageResize.HashOriginalContent;
        options.Cache.FolderSharding = imageResize.Cache.FolderSharding;
        options.Cache.PruneOnStartup = imageResize.Cache.PruneOnStartup;
        options.Cache.MaxCacheBytes = imageResize.Cache.MaxCacheBytes;
        options.ResponseCache.ClientCacheSeconds = imageResize.ResponseCache.ClientCacheSeconds;
        options.ResponseCache.SendETag = imageResize.ResponseCache.SendETag;
        options.ResponseCache.SendLastModified = imageResize.ResponseCache.SendLastModified;
    }
}

/// <summary>The password rules and <c>SignInRequireConfirmedAccount</c> of <c>Zauber:Identity</c>.</summary>
internal sealed class IdentityOptionsFromZauberSettings(IOptions<ZauberSettings> settings)
    : IConfigureOptions<IdentityOptions>
{
    public void Configure(IdentityOptions options)
    {
        var identity = settings.Value.Identity;
        options.Password.RequireDigit = identity.PasswordRequireDigit;
        options.Password.RequireLowercase = identity.PasswordRequireLowercase;
        options.Password.RequireNonAlphanumeric = identity.PasswordRequireNonAlphanumeric;
        options.Password.RequireUppercase = identity.PasswordRequireUppercase;
        options.Password.RequiredLength = identity.PasswordRequiredLength;
        options.Password.RequiredUniqueChars = identity.PasswordRequiredUniqueChars;
        options.SignIn.RequireConfirmedAccount = identity.SignInRequireConfirmedAccount;
    }
}

/// <summary>
/// The <c>login</c> and <c>register</c> rate-limit policies, from <c>Zauber:Identity:LoginRateLimit</c> and
/// <c>RegisterRateLimit</c> (ZA-149). A <c>PermitLimit</c> of 0 turns that limit off.
/// </summary>
internal sealed class RateLimiterOptionsFromZauberSettings(IOptions<ZauberSettings> settings)
    : IConfigureOptions<RateLimiterOptions>
{
    public void Configure(RateLimiterOptions options)
    {
        var loginLimit = settings.Value.Identity.LoginRateLimit;
        var registerLimit = settings.Value.Identity.RegisterRateLimit;
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

        // Sliding window rate limiter for login attempts (10 per address in 5 minutes by default)
        options.AddPolicy("login", httpContext =>
            loginLimit.PermitLimit > 0
                ? RateLimitPartition.GetSlidingWindowLimiter(
                    partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    factory: _ => loginLimit.ToLimiterOptions())
                : RateLimitPartition.GetNoLimiter("login-off"));

        // Registration has a limit of its own, so a flood of sign-ups neither uses up the login allowance of the
        // people behind the same address nor hides inside it. Only the posts count: looking at the form is free,
        // sending it is not (5 per address in 10 minutes by default).
        options.AddPolicy(ZauberSetup.RegisterRateLimitPolicy, httpContext =>
            HttpMethods.IsPost(httpContext.Request.Method) && registerLimit.PermitLimit > 0
                ? RateLimitPartition.GetSlidingWindowLimiter(
                    partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    factory: _ => registerLimit.ToLimiterOptions())
                : RateLimitPartition.GetNoLimiter("register-view"));
    }
}

/// <summary>
/// The Redis address for the distributed cache. Whether Redis is used at all is decided from configuration while
/// the services are registered (D81); this gives the address, so a site can set it in code (from a secret store).
/// </summary>
internal sealed class RedisCacheOptionsFromZauberSettings(IOptions<ZauberSettings> settings)
    : IConfigureOptions<RedisCacheOptions>
{
    public void Configure(RedisCacheOptions options) => options.Configuration = settings.Value.RedisConnectionString;
}

/// <summary>
/// <c>Zauber:ShowDetailedErrors</c> turns on Blazor's detailed errors, never in Production: exception messages and
/// stack traces in a visitor's browser turn a small bug into an information leak. Only turns them on, so the
/// framework's own <c>DetailedErrors</c> setting keeps working. A post-configure step: the framework's own
/// post-configure of <see cref="RazorComponentsServiceOptions"/> sets <c>DetailedErrors</c> from that setting alone,
/// which undid ZauberCMS's for static rendering (ZA-230).
/// </summary>
internal sealed class DetailedErrorsFromZauberSettings(IOptions<ZauberSettings> settings, IHostEnvironment environment)
    : IPostConfigureOptions<CircuitOptions>, IPostConfigureOptions<RazorComponentsServiceOptions>
{
    private bool Enabled => settings.Value.ShowDetailedErrors && !environment.IsProduction();

    public void PostConfigure(string? name, CircuitOptions options)
    {
        if (Enabled)
        {
            options.DetailedErrors = true;
        }
    }

    public void PostConfigure(string? name, RazorComponentsServiceOptions options)
    {
        if (Enabled)
        {
            options.DetailedErrors = true;
        }
    }
}
