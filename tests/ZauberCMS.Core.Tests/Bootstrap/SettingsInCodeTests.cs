using System.Net;
using ImageResize.Core.Configuration;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Components.Endpoints;
using Microsoft.AspNetCore.Components.Server;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ZauberCMS.Core.Settings;
using ZauberCMS.Core.Tests.Infrastructure;

namespace ZauberCMS.Core.Tests.Bootstrap;

/// <summary>
/// ZA-192 (Roadmap D81): settings a site changes in code with <c>Configure&lt;ZauberSettings&gt;</c> reach the options
/// ZauberCMS sets up for other libraries (ImageResize, Identity, the rate limits, Redis, detailed errors), as the
/// configured ones do. The settings that decide what is registered (the database, Redis on or off) come from
/// configuration; a change to them in code is a start-up warning, or stops the start when it cannot work.
/// </summary>
public class SettingsInCodeTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ZA192_image_resize_settings_set_in_code_reach_the_resizer()
    {
        var cacheRoot = Path.Combine(Path.GetTempPath(), "zauber-tests", "code-cache");
        await using var app = BuildApp(afterAddZauberCms: builder => builder.Services.Configure<ZauberSettings>(s =>
        {
            s.ImageResize.AllowUpscale = true;
            s.ImageResize.DefaultQuality = 77;
            s.ImageResize.CacheRoot = cacheRoot;
            s.ImageResize.ContentRoots = ["uploads"];
            s.ImageResize.Cache.MaxCacheBytes = 1234;
        }));

        var options = app.Services.GetRequiredService<IOptions<ImageResizeOptions>>().Value;

        Assert.True(options.AllowUpscale);
        Assert.Equal(77, options.DefaultQuality);
        Assert.Equal(cacheRoot, options.CacheRoot);
        Assert.Equal(["uploads"], options.ContentRoots);
        Assert.Equal(1234, options.Cache.MaxCacheBytes);
        // What is not set falls back to the host as before
        Assert.Equal(app.Environment.WebRootPath, options.WebRoot);
    }

    [Fact]
    public async Task ZA192_without_code_the_resizer_has_the_configured_and_default_values()
    {
        await using var app = BuildApp(new Dictionary<string, string?> { ["Zauber:ImageResize:DefaultQuality"] = "66" });

        var options = app.Services.GetRequiredService<IOptions<ImageResizeOptions>>().Value;

        Assert.Equal(66, options.DefaultQuality);
        Assert.False(options.AllowUpscale); // D36
        Assert.Equal(Path.Combine(app.Environment.WebRootPath, "_mediacache"), options.CacheRoot);
    }

    [Fact]
    public async Task ZA192_identity_settings_set_in_code_reach_the_identity_options()
    {
        await using var app = BuildApp(afterAddZauberCms: builder => builder.Services.Configure<ZauberSettings>(s =>
        {
            s.Identity.PasswordRequiredLength = 14;
            s.Identity.PasswordRequireNonAlphanumeric = true;
            s.Identity.SignInRequireConfirmedAccount = true;
        }));

        var options = app.Services.GetRequiredService<IOptions<IdentityOptions>>().Value;

        Assert.Equal(14, options.Password.RequiredLength);
        Assert.True(options.Password.RequireNonAlphanumeric);
        Assert.True(options.SignIn.RequireConfirmedAccount);
        // The fixed ones stay
        Assert.Equal(6, options.Lockout.MaxFailedAccessAttempts);
        Assert.True(options.User.RequireUniqueEmail);

        // And the user manager checks passwords with them
        using var scope = app.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ZauberCMS.Core.Membership.Models.User>>();
        Assert.Equal(14, userManager.Options.Password.RequiredLength);
    }

    [Fact]
    public async Task ZA192_a_sites_own_identity_options_after_AddZauberCms_still_win()
    {
        await using var app = BuildApp(afterAddZauberCms: builder =>
        {
            builder.Services.Configure<ZauberSettings>(s => s.Identity.PasswordRequiredLength = 14);
            builder.Services.Configure<IdentityOptions>(o => o.Password.RequiredLength = 20);
        });

        Assert.Equal(20, app.Services.GetRequiredService<IOptions<IdentityOptions>>().Value.Password.RequiredLength);
    }

    [Fact]
    public async Task ZA192_a_login_rate_limit_set_in_code_limits_the_sign_in_pages()
    {
        var contentRoot = TestSite.CreateContentRoot();
        await using var app = TestSite.BuildApp("", "", contentRoot, validateContainer: false,
            new Dictionary<string, string?> { ["urls"] = "http://127.0.0.1:0" },
            afterAddZauberCms: builder => builder.Services.Configure<ZauberSettings>(s =>
                s.Identity.LoginRateLimit = new RateLimitSettings { PermitLimit = 1, WindowMinutes = 5 }));
        app.UseRateLimiter();
        app.MapGet("/test/login", () => "ok").RequireRateLimiting("login");
        app.MapGet("/test/register", () => "ok").RequireRateLimiting(ZauberSetup.RegisterRateLimitPolicy);
        await app.StartAsync(Ct);
        try
        {
            using var client = new HttpClient
            {
                BaseAddress = new Uri(app.Services.GetRequiredService<IServer>().Features
                    .Get<IServerAddressesFeature>()!.Addresses.First())
            };

            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/test/login", Ct)).StatusCode);
            Assert.Equal(HttpStatusCode.TooManyRequests, (await client.GetAsync("/test/login", Ct)).StatusCode);
            // Looking at the registration form is free, as before
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/test/register", Ct)).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/test/register", Ct)).StatusCode);
        }
        finally
        {
            await app.StopAsync(Ct);
            TryDelete(contentRoot);
        }

        Assert.Equal(StatusCodes.Status429TooManyRequests,
            app.Services.GetRequiredService<IOptions<RateLimiterOptions>>().Value.RejectionStatusCode);
    }

    [Fact]
    public async Task ZA192_a_redis_address_set_in_code_is_the_one_used_when_configuration_turns_redis_on()
    {
        await using var app = BuildApp(new Dictionary<string, string?> { ["Zauber:RedisConnectionString"] = "configured:6379" },
            builder => builder.Services.Configure<ZauberSettings>(s => s.RedisConnectionString = "from-a-vault:6379"));

        Assert.Equal("from-a-vault:6379",
            app.Services.GetRequiredService<IOptions<RedisCacheOptions>>().Value.Configuration);
        Assert.Empty(ZauberSettingsValidator.FindChangesThatDoNotApply(Composed(app), Configured(app), Options(app)));
        Assert.Null(ZauberSettingsValidator.ChangeThatCannotWork(Composed(app), Configured(app), Options(app)));
    }

    [Theory]
    [InlineData("Testing", true)]
    [InlineData("Development", true)]
    [InlineData("Production", false)]
    public async Task ZA192_detailed_errors_set_in_code_turn_on_outside_production(string environment, bool expected)
    {
        await using var app = BuildApp(afterAddZauberCms: builder =>
            builder.Services.Configure<ZauberSettings>(s => s.ShowDetailedErrors = true), environment: environment);

        Assert.Equal(expected, app.Services.GetRequiredService<IOptions<CircuitOptions>>().Value.DetailedErrors);
        Assert.Equal(expected,
            app.Services.GetRequiredService<IOptions<RazorComponentsServiceOptions>>().Value.DetailedErrors);
    }

    [Fact]
    public async Task ZA192_detailed_errors_stay_off_by_default()
    {
        await using var app = BuildApp();

        Assert.False(app.Services.GetRequiredService<IOptions<CircuitOptions>>().Value.DetailedErrors);
        Assert.False(app.Services.GetRequiredService<IOptions<RazorComponentsServiceOptions>>().Value.DetailedErrors);
    }

    // ---- what is read while the services are registered ----

    [Fact]
    public async Task ZA192_settings_left_alone_in_code_give_no_warning()
    {
        await using var app = BuildApp();

        Assert.Empty(ZauberSettingsValidator.FindChangesThatDoNotApply(Composed(app), Configured(app), Options(app)));
        Assert.Null(ZauberSettingsValidator.ChangeThatCannotWork(Composed(app), Configured(app), Options(app)));
    }

    [Fact]
    public async Task ZA192_a_database_or_redis_set_in_code_is_a_startup_warning_that_shows_no_value()
    {
        var logger = new RecordingLoggerProvider();
        await using var app = BuildApp(afterAddZauberCms: builder =>
        {
            builder.Services.AddSingleton<ILoggerFactory>(new LoggerFactory([logger]));
            builder.Services.Configure<ZauberSettings>(s =>
            {
                s.DatabaseProvider = "SqlServer";
                s.ConnectionString = "Server=db;Password=secret-in-code";
                s.RedisConnectionString = "redis-in-code:6379";
            });
        });

        var settings = ZauberSetup.ReadSettingsAndWriteStartupWarnings(app.Services);

        Assert.Equal("SqlServer", settings.DatabaseProvider);
        var warnings = logger.Entries
            .Where(e => e is { Category: "ZauberCMS.Startup", Level: LogLevel.Warning })
            .Select(e => e.Message)
            .ToList();
        Assert.Contains(warnings, w => w.StartsWith("Zauber:DatabaseProvider is changed in code", StringComparison.Ordinal));
        Assert.Contains(warnings, w => w.StartsWith("Zauber:ConnectionString is changed in code", StringComparison.Ordinal));
        Assert.Contains(warnings, w => w.StartsWith("Zauber:RedisConnectionString is set in code", StringComparison.Ordinal));
        Assert.DoesNotContain(warnings, w => w.Contains("secret-in-code", StringComparison.Ordinal) ||
                                             w.Contains("redis-in-code", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ZA192_redis_emptied_in_code_while_configuration_turns_it_on_stops_the_start()
    {
        var logger = new RecordingLoggerProvider();
        await using var app = BuildApp(new Dictionary<string, string?> { ["Zauber:RedisConnectionString"] = "configured:6379" },
            builder =>
            {
                builder.Services.AddSingleton<ILoggerFactory>(new LoggerFactory([logger]));
                builder.Services.Configure<ZauberSettings>(s => s.RedisConnectionString = null);
            });

        var error = Assert.Throws<InvalidOperationException>(() => ZauberSetup.ReadSettingsAndWriteStartupWarnings(app.Services));

        Assert.StartsWith("Zauber:RedisConnectionString was set when AddZauberCms(builder) registered the services",
            error.Message, StringComparison.Ordinal);
        Assert.Contains("emptied in code (Configure<ZauberSettings>)", error.Message, StringComparison.Ordinal);
        Assert.Contains(logger.Entries, e => e is { Category: "ZauberCMS.Startup", Level: LogLevel.Critical });
    }

    // ---- ZA-231: a configuration source added after AddZauberCms(builder) is not a change made in code ----

    [Fact]
    public async Task ZA231_a_connection_string_from_a_later_configuration_source_is_used_and_gives_no_warning()
    {
        var logger = new RecordingLoggerProvider();
        await using var app = BuildApp(afterAddZauberCms: builder =>
        {
            builder.Services.AddSingleton<ILoggerFactory>(new LoggerFactory([logger]));
            // As a site adds Key Vault, a local JSON file or an Aspire connection string below AddZauberCms
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Zauber:ConnectionString"] = "DataSource=late-source;Mode=Memory;Cache=Shared"
            });
        });

        ZauberSetup.ReadSettingsAndWriteStartupWarnings(app.Services);

        Assert.DoesNotContain(logger.Entries, e => e.Message.StartsWith("Zauber:ConnectionString", StringComparison.Ordinal));
        using var scope = app.Services.CreateScope();
        var dbContext = (Microsoft.EntityFrameworkCore.DbContext)scope.ServiceProvider
            .GetRequiredService<ZauberCMS.Core.Data.IZauberDbContext>();
        Assert.Equal("DataSource=late-source;Mode=Memory;Cache=Shared",
            Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions.GetConnectionString(dbContext.Database));
    }

    [Fact]
    public async Task ZA231_redis_set_by_a_later_configuration_source_warns_naming_the_configuration()
    {
        var logger = new RecordingLoggerProvider();
        await using var app = BuildApp(afterAddZauberCms: builder =>
        {
            builder.Services.AddSingleton<ILoggerFactory>(new LoggerFactory([logger]));
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Zauber:RedisConnectionString"] = "late-source:6379"
            });
        });

        ZauberSetup.ReadSettingsAndWriteStartupWarnings(app.Services);

        var warning = Assert.Single(logger.Entries, e => e.Message.StartsWith("Zauber:RedisConnectionString", StringComparison.Ordinal));
        Assert.StartsWith("Zauber:RedisConnectionString is set by a configuration source added after AddZauberCms(builder)",
            warning.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("late-source", warning.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ZA231_redis_emptied_by_a_later_configuration_source_stops_the_start_naming_the_configuration()
    {
        await using var app = BuildApp(new Dictionary<string, string?> { ["Zauber:RedisConnectionString"] = "configured:6379" },
            builder => builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Zauber:RedisConnectionString"] = ""
            }));

        var error = Assert.Throws<InvalidOperationException>(() => ZauberSetup.ReadSettingsAndWriteStartupWarnings(app.Services));

        Assert.Contains("emptied by a configuration source added after AddZauberCms(builder)", error.Message,
            StringComparison.Ordinal);
    }

    private static ZauberSettings Composed(WebApplication app) =>
        app.Services.GetRequiredService<ComposedZauberSettings>().Settings;

    private static ZauberSettings Configured(WebApplication app) =>
        ZauberSettingsBinding.Read(app.Configuration.GetSection(Constants.SettingsConfigName));

    private static ZauberSettings Options(WebApplication app) =>
        app.Services.GetRequiredService<IOptions<ZauberSettings>>().Value;

    private static WebApplication BuildApp(IDictionary<string, string?>? extraSettings = null,
        Action<WebApplicationBuilder>? afterAddZauberCms = null, string environment = "Testing") =>
        TestSite.BuildApp("Sqlite", "DataSource=:memory:", TestSite.CreateContentRoot(), validateContainer: false,
            extraSettings, afterAddZauberCms: afterAddZauberCms, environmentName: environment);

    private static void TryDelete(string directory)
    {
        try
        {
            Directory.Delete(directory, recursive: true);
        }
        catch (IOException)
        {
            // A file still open on Windows: the temp folder is cleaned up later
        }
        catch (UnauthorizedAccessException)
        {
            // As above
        }
    }

    private sealed class RecordingLoggerProvider : ILoggerProvider
    {
        public List<(string Category, LogLevel Level, string Message)> Entries { get; } = [];

        public ILogger CreateLogger(string categoryName) => new Recorder(this, categoryName);

        public void Dispose()
        {
        }

        private sealed class Recorder(RecordingLoggerProvider owner, string category) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
                Func<TState, Exception?, string> formatter)
            {
                lock (owner.Entries)
                {
                    owner.Entries.Add((category, logLevel, formatter(state, exception)));
                }
            }
        }
    }
}
