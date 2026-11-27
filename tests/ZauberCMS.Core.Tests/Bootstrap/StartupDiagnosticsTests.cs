using System.Globalization;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.Loader;
using ImageResize.Core.Configuration;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ZauberCMS.Core.Data;
using ZauberCMS.Core.Data.Interfaces;
using ZauberCMS.Core.Membership;
using ZauberCMS.Core.Membership.Models;
using ZauberCMS.Core.Plugins;
using ZauberCMS.Core.Plugins.Interfaces;
using ZauberCMS.Core.Providers;
using ZauberCMS.Core.Settings;
using ZauberCMS.Core.Tests.Infrastructure;

namespace ZauberCMS.Core.Tests.Bootstrap;

/// <summary>
/// Roadmap Phase 11d: discovery and settings that failed silently or late (ZA-120, D48), DI leftovers (ZA-121), and a
/// list setting that was added to its default instead of replacing it (ZA-167).
/// </summary>
public class StartupDiagnosticsTests
{
    private const string DiskStorage = "ZauberCMS.Core.Providers.DiskStorageProvider";
    private const string SmtpEmail = "ZauberCMS.Core.Providers.SmtpEmailProvider";

    // ---- ZA-120: settings that cannot work stop the start, with the setting's name ----

    [Theory]
    [InlineData("StorageProvider", DiskStorage)]
    [InlineData("EmailProvider", SmtpEmail)]
    public async Task ZA120_a_mistyped_provider_stops_the_start_naming_the_setting_and_the_providers(string setting,
        string realProvider)
    {
        await using var app = BuildApp(new Dictionary<string, string?>
        {
            [$"Zauber:Plugins:{setting}"] = realProvider + "s"
        });

        // AddZauberCms(app) reads the settings first; ValidateOnStart covers a host started without it
        var error = Assert.Throws<OptionsValidationException>(() =>
            app.Services.GetRequiredService<IOptions<ZauberSettings>>().Value);
        Assert.Contains($"Zauber:Plugins:{setting} is '{realProvider}s'", error.Message, StringComparison.Ordinal);
        Assert.Contains("Found: ", error.Message, StringComparison.Ordinal);
        Assert.Contains(realProvider, error.Message.Replace(realProvider + "s'", "", StringComparison.Ordinal),
            StringComparison.Ordinal);
        Assert.Throws<OptionsValidationException>(() =>
            app.Services.GetRequiredService<IStartupValidator>().Validate());
    }

    [Fact]
    public async Task ZA120_a_mistyped_culture_stops_the_start_naming_the_setting()
    {
        await using var app = BuildApp(new Dictionary<string, string?> { ["Zauber:AdminDefaultLanguage"] = "en US!" });

        var error = Assert.Throws<OptionsValidationException>(() =>
            app.Services.GetRequiredService<IOptions<ZauberSettings>>().Value);
        Assert.Contains("Zauber:AdminDefaultLanguage is 'en US!'", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ZA120_the_defaults_a_blank_provider_and_a_site_without_a_database_still_start()
    {
        // Nothing configured but the database: every default of ZauberSettings passes
        await using (var defaults = TestSite.BuildApp("Sqlite", "DataSource=:memory:", TestSite.CreateContentRoot(),
                         validateContainer: false))
        {
            defaults.Services.GetRequiredService<IStartupValidator>().Validate();
            Assert.Equal(DiskStorage, defaults.Services.GetRequiredService<IOptions<ZauberSettings>>().Value.Plugins.StorageProvider);
        }

        // An empty database setting is the installer (D24), not an error
        await using (var installer = TestSite.BuildApp("", "", TestSite.CreateContentRoot(), validateContainer: false))
        {
            installer.Services.GetRequiredService<IStartupValidator>().Validate();
        }

        // A blank provider is no provider, as a missing key always was
        await using var blank = BuildApp(new Dictionary<string, string?> { ["Zauber:Plugins:EmailProvider"] = "" });
        blank.Services.GetRequiredService<IStartupValidator>().Validate();
        using var scope = blank.Services.CreateScope();
        Assert.Null(scope.ServiceProvider.GetRequiredService<ProviderService>().EmailProvider);
    }

    [Theory]
    [InlineData("ZauberCMS/appsettings.json")]
    [InlineData("ZauberCMS.Web/appsettings.json")]
    [InlineData("ZauberCMS.Template/template/ZauberCMSTemplate.Site/appsettings.json")]
    public async Task ZA120_the_shipped_settings_pass_and_warn_about_nothing(string relativePath)
    {
        await using var _ = BuildApp(); // the bootstrap sets the scanned assemblies
        var configuration = new ConfigurationBuilder()
            .AddJsonFile(Path.Combine(RepositoryRoot(), relativePath), optional: false, reloadOnChange: false)
            .Build();
        var settings = ZauberSettingsBinding.Read(configuration.GetSection(Constants.SettingsConfigName));

        using var services = new ServiceCollection().BuildServiceProvider();
        var result = new ZauberSettingsValidator(services).Validate(null, settings);

        Assert.True(result.Succeeded, result.FailureMessage);
        Assert.Empty(ZauberSettingsValidator.FindWarnings(settings, new ExtensionManager(services)));
    }

    [Fact]
    public async Task ZA120_settings_that_do_nothing_as_written_are_warnings()
    {
        await using var _ = BuildApp(); // the bootstrap sets the scanned assemblies
        using var services = new ServiceCollection().BuildServiceProvider();
        var settings = new ZauberSettings
        {
            SiteUrl = "www.example.com",
            MissingView = "My.Site.Views.Missing",
            Identity = { AccountLayout = "My.Site.AccountLayout" }
        };

        var warnings = ZauberSettingsValidator.FindWarnings(settings, new ExtensionManager(services)).ToList();

        Assert.Equal(3, warnings.Count);
        Assert.Contains(warnings, w => w.StartsWith("Zauber:SiteUrl is 'www.example.com'", StringComparison.Ordinal));
        Assert.Contains(warnings, w => w.StartsWith("Zauber:MissingView is 'My.Site.Views.Missing'", StringComparison.Ordinal));
        Assert.Contains(warnings, w => w.StartsWith("Zauber:Identity:AccountLayout is", StringComparison.Ordinal));
        Assert.Empty(ZauberSettingsValidator.FindWarnings(new ZauberSettings(), new ExtensionManager(services)));
    }

    [Fact]
    public async Task ZA120_a_mistyped_provider_is_still_named_when_it_is_asked_for()
    {
        // ProviderService gives the same message when the setting got past start-up (a site that removed the validator)
        await using var app = BuildApp();
        using var scope = app.Services.CreateScope();
        var settings = new ZauberSettings { Plugins = { StorageProvider = "Nope.Storage" } };
        var providers = new ProviderService(Options.Create(settings),
            scope.ServiceProvider.GetRequiredService<ExtensionManager>());

        var error = Assert.Throws<InvalidOperationException>(() => providers.StorageProvider);
        Assert.StartsWith("Zauber:Plugins:StorageProvider is 'Nope.Storage'", error.Message, StringComparison.Ordinal);
        Assert.Contains(DiskStorage, error.Message, StringComparison.Ordinal);
    }

    // ---- ZA-120: what discovery skips is logged once logging is up ----

    [Fact]
    public async Task ZA120_bootstrap_skips_and_hidden_extension_types_are_collected_then_logged_once_logging_is_up()
    {
        var logger = new RecordingLoggerProvider();
        var contentRoot = TestSite.CreateContentRoot();
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = "Testing",
            ContentRootPath = contentRoot,
            WebRootPath = Path.Combine(contentRoot, "wwwroot")
        });
        builder.Configuration.Sources.Clear();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            // No database: AddZauberCms(app) runs without one and the site would show the installer
            ["Zauber:DatabaseProvider"] = "",
            ["Zauber:ConnectionString"] = "",
            ["Zauber:SiteUrl"] = "not-an-address"
        });
        builder.AddZauberCms();

        // Collected while AddZauberCms(builder) ran, before there was any logger
        var collected = builder.Services.Single(d => d.ServiceType == typeof(StartupMessages)).ImplementationInstance;
        var messages = Assert.IsType<StartupMessages>(collected);
        Assert.Contains(messages.Warnings, w => w.StartsWith(typeof(StartupPluginWithoutDefaultConstructor).FullName +
            " implements IStartupPlugin but has no parameterless constructor", StringComparison.Ordinal));
        Assert.Contains(messages.Warnings, w => w.StartsWith(typeof(HiddenSeedData).FullName +
            " implements ISeedData but is not public", StringComparison.Ordinal));

        builder.Services.AddSingleton<ILoggerFactory>(new LoggerFactory([logger]));
        await using var app = builder.Build();

        // What AddZauberCms(app) does first, once the settings are read (the rest of it needs the site's static
        // assets manifest, which a test assembly has not got)
        ZauberSetup.WriteStartupWarnings(app.Services, app.Services.GetRequiredService<IOptions<ZauberSettings>>().Value);

        var startup = logger.Entries.Where(e => e.Category == "ZauberCMS.Startup").ToList();
        Assert.All(startup, e => Assert.Equal(LogLevel.Warning, e.Level));
        Assert.Contains(startup, e => e.Message.Contains("has no parameterless constructor", StringComparison.Ordinal));
        Assert.Contains(startup, e => e.Message.Contains("is not public", StringComparison.Ordinal));
        Assert.Contains(startup, e => e.Message.StartsWith("Zauber:SiteUrl is 'not-an-address'", StringComparison.Ordinal));
        Assert.Empty(messages.Warnings);
    }

    [Fact]
    public void ZA120_every_discovery_interface_in_the_list_exists()
    {
        var assemblies = new[]
        {
            typeof(ZauberSetup).Assembly, typeof(Components.Pages.EntryPage).Assembly,
            typeof(FluentValidation.IValidator<>).Assembly
        };
        foreach (var name in ExtensionManager.DiscoveryInterfaces)
        {
            var type = assemblies.Select(a => a.GetType(name)).SingleOrDefault(t => t != null);
            Assert.True(type is { IsInterface: true, IsPublic: true }, $"{name} is not a public interface");
        }
    }

    // ---- ZA-120: the discovery cache is keyed by what was asked ----

    [Fact]
    public async Task ZA120_a_cached_search_is_not_returned_for_a_predicate_or_for_non_public_types()
    {
        await using var app = BuildApp();
        using var scope = app.Services.CreateScope();
        var extensionManager = scope.ServiceProvider.GetRequiredService<ExtensionManager>();

        var all = extensionManager.GetImplementations<ISeedData>(useCaching: true);
        Assert.NotEmpty(all);
        Assert.DoesNotContain(typeof(HiddenSeedData).FullName!, all.Keys);

        // Before the fix each of these got the cached dictionary of the first call
        Assert.Empty(extensionManager.GetImplementations<ISeedData>(_ => false, useCaching: true));
        Assert.Contains(typeof(HiddenSeedData).FullName!,
            extensionManager.GetImplementations<ISeedData>(useCaching: true, onlyExportedTypes: false).Keys);

        // And a caller that changes its dictionary does not change the cache
        all.Clear();
        Assert.NotEmpty(extensionManager.GetImplementations<ISeedData>(useCaching: true));
    }

    [Fact]
    public void ZA182_a_fluent_validator_that_is_not_public_is_a_startup_warning()
    {
        // FluentValidation's scan finds public validators only, so this one never runs
        var messages = new StartupMessages();
        ExtensionManager.ReportHiddenExtensionTypes([typeof(HiddenValidator).Assembly], messages);

        Assert.Contains(messages.Warnings, w => w.StartsWith(typeof(HiddenValidator).FullName +
            " implements IValidator`1 but is not public", StringComparison.Ordinal));
    }

    // ---- ZA-176: a start that fails says why, in the log and naming the plugin ----

    [Fact]
    public async Task ZA176_the_startup_warnings_and_a_failing_setting_reach_the_log_before_the_start_stops()
    {
        var logger = new RecordingLoggerProvider();
        var contentRoot = TestSite.CreateContentRoot();
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = "Testing",
            ContentRootPath = contentRoot,
            WebRootPath = Path.Combine(contentRoot, "wwwroot")
        });
        builder.Configuration.Sources.Clear();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Zauber:DatabaseProvider"] = "",
            ["Zauber:ConnectionString"] = "",
            ["Zauber:AdminDefaultLanguage"] = "en US!"
        });
        builder.AddZauberCms();
        builder.Services.AddSingleton<ILoggerFactory>(new LoggerFactory([logger]));
        await using var app = builder.Build();

        // What AddZauberCms(app) does first; before the fix the collected warnings were lost and nothing was logged
        var error = Assert.Throws<OptionsValidationException>(() =>
            ZauberSetup.ReadSettingsAndWriteStartupWarnings(app.Services));

        var startup = logger.Entries.Where(e => e.Category == "ZauberCMS.Startup").ToList();
        Assert.Contains(startup, e => e.Level == LogLevel.Warning &&
                                      e.Message.Contains("has no parameterless constructor", StringComparison.Ordinal));
        var critical = Assert.Single(startup, e => e.Level == LogLevel.Critical);
        Assert.Contains("Zauber:AdminDefaultLanguage is 'en US!'", critical.Message, StringComparison.Ordinal);
        Assert.Contains("Zauber:AdminDefaultLanguage is 'en US!'", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ZA176_a_plugin_whose_public_type_cannot_load_stops_the_start_naming_the_plugin_and_the_type()
    {
        var plugin = EmitPluginBuiltAgainstZauber4("ZA176PublicPlugin", publicEditor: true);

        // Before the fix the bootstrap only warned ("discovery skips them") and every discovery call then threw a
        // TypeLoadException that named ZauberCMS.Core, not the plugin
        var error = Assert.Throws<InvalidOperationException>(() =>
            TestSite.BuildApp("Sqlite", "DataSource=:memory:", TestSite.CreateContentRoot(), validateContainer: false,
                additionalAssemblies: [plugin]));

        Assert.StartsWith("ZA176PublicPlugin cannot be used", error.Message, StringComparison.Ordinal);
        Assert.Contains("ZauberCMS.Core.Content.Interfaces.IContentProperty", error.Message, StringComparison.Ordinal);
        Assert.Contains("built against another ZauberCMS version", error.Message, StringComparison.Ordinal);
        Assert.IsType<TypeLoadException>(error.InnerException);
    }

    [Fact]
    public void ZA176_a_plugin_whose_only_unloadable_types_are_not_public_still_starts_with_a_warning()
    {
        var plugin = EmitPluginBuiltAgainstZauber4("ZA176InternalPlugin", publicEditor: false);

        ExtensionManager.EnsurePublicTypesLoad(plugin); // does not throw: a site that starts today keeps starting

        var messages = new StartupMessages();
        ExtensionManager.ReportHiddenExtensionTypes([plugin], messages);
        Assert.Contains(messages.Warnings, w =>
            w.StartsWith("1 types of ZA176InternalPlugin could not be loaded, so discovery skips them",
                StringComparison.Ordinal));
    }

    [Fact]
    public void ZA176_a_missing_dependency_also_names_the_plugin()
    {
        var error = Assert.Throws<InvalidOperationException>(() => ExtensionManager.EnsurePublicTypesLoad(
            "My.Plugin", () => throw new FileNotFoundException("Could not load it.", "Some.Dependency, Version=1.0.0.0")));

        Assert.StartsWith("My.Plugin cannot be used: its public types need the assembly Some.Dependency", error.Message,
            StringComparison.Ordinal);
        ExtensionManager.EnsurePublicTypesLoad("My.Plugin", () => [typeof(string)]);
    }

    // ---- ZA-179: the culture error carries the runtime's reason ----

    [Fact]
    public async Task ZA179_an_admin_language_that_is_not_a_culture_gives_the_runtimes_reason()
    {
        await using var app = BuildApp(new Dictionary<string, string?> { ["Zauber:AdminDefaultLanguage"] = "en US!" });

        var error = Assert.Throws<OptionsValidationException>(() =>
            app.Services.GetRequiredService<IOptions<ZauberSettings>>().Value);
        Assert.Contains("is an invalid culture identifier", error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain('\n', error.Message);
    }

    [Fact]
    public void ZA179_in_globalization_invariant_mode_the_message_gives_the_runtimes_reason()
    {
        // What the runtime throws for "en-US" in invariant mode (a test process cannot switch the mode on)
        var invariant = new CultureNotFoundException("name", "en-US",
            "Only the invariant culture is supported in globalization-invariant mode. See " +
            "https://aka.ms/GlobalizationInvariantMode for more information.");

        var message = ZauberSettingsValidator.AdminDefaultLanguageFailure("en-US", invariant, globalizationInvariant: true);

        Assert.StartsWith("Zauber:AdminDefaultLanguage is 'en-US'", message, StringComparison.Ordinal);
        Assert.Contains("Only the invariant culture is supported in globalization-invariant mode", message,
            StringComparison.Ordinal);
        Assert.Contains("DOTNET_SYSTEM_GLOBALIZATION_INVARIANT", message, StringComparison.Ordinal);
        // Before the fix it said "which is not a culture name such as en-US", about en-US
        Assert.DoesNotContain("such as en-US", message, StringComparison.Ordinal);
    }

    // ---- ZA-121: DI leftovers ----

    [Fact]
    public async Task ZA121_the_sign_in_manager_is_registered_once_and_the_leftovers_are_gone()
    {
        await using var app = BuildApp();
        using var scope = app.Services.CreateScope();
        var services = scope.ServiceProvider;

        var signInManager = Assert.Single(services.GetServices<SignInManager<User>>());
        Assert.IsType<ZauberSignInManager>(signInManager);
        Assert.Same(services.GetRequiredService<SignInManager<User>>(), services.GetRequiredService<ZauberSignInManager>());

        // HttpClient is the framework's default client from IHttpClientFactory (AddHttpClient), no longer one with the
        // request's own address as its base
        Assert.Null(services.GetRequiredService<HttpClient>().BaseAddress);
        Assert.NotNull(services.GetService<IHttpClientFactory>());

        Assert.Null(services.GetService<IUserEmailStore<User>>());
        Assert.True(services.GetRequiredService<UserManager<User>>().SupportsUserEmail);

#pragma warning disable CS0618 // the obsolete path: still resolvable until it is removed
        Assert.NotNull(services.GetService<IdentityUserAccessor>());
#pragma warning restore CS0618
    }

    // ---- ZA-167: a configured list replaces its default ----

    [Fact]
    public async Task ZA167_configured_content_roots_replace_the_defaults()
    {
        await using var app = BuildApp(new Dictionary<string, string?>
        {
            ["Zauber:ImageResize:ContentRoots:0"] = "uploads"
        });

        Assert.Equal(["uploads"], app.Services.GetRequiredService<IOptions<ZauberSettings>>().Value.ImageResize.ContentRoots);
        Assert.Equal(["uploads"], app.Services.GetRequiredService<IOptions<ImageResizeOptions>>().Value.ContentRoots);
    }

    [Fact]
    public async Task ZA167_without_the_key_the_defaults_stay_and_the_shipped_list_appears_once()
    {
        await using (var none = BuildApp())
        {
            Assert.Equal(["img", "images", "media"],
                none.Services.GetRequiredService<IOptions<ImageResizeOptions>>().Value.ContentRoots);
        }

        // What the shipped appsettings.json files set: the same three, now once each (they were there twice)
        await using var shipped = BuildApp(new Dictionary<string, string?>
        {
            ["Zauber:ImageResize:ContentRoots:0"] = "img",
            ["Zauber:ImageResize:ContentRoots:1"] = "images",
            ["Zauber:ImageResize:ContentRoots:2"] = "media"
        });
        Assert.Equal(["img", "images", "media"],
            shipped.Services.GetRequiredService<IOptions<ZauberSettings>>().Value.ImageResize.ContentRoots);
        Assert.Equal(["img", "images", "media"],
            shipped.Services.GetRequiredService<IOptions<ImageResizeOptions>>().Value.ContentRoots);
    }

    [Fact]
    public void ZA167_every_list_setting_with_a_default_is_replaced_when_configured()
    {
        // A guard for list settings added later: each one with a default must take the configured list as it is
        var lists = ListSettingsWithDefaults(typeof(ZauberSettings), new ZauberSettings(), "").ToList();
        Assert.NotEmpty(lists);

        foreach (var path in lists)
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { [$"Zauber:{path}:0"] = "configured" })
                .Build();
            var settings = ZauberSettingsBinding.Read(configuration.GetSection(Constants.SettingsConfigName));

            object? value = settings;
            foreach (var part in path.Split(':'))
            {
                value = value!.GetType().GetProperty(part)!.GetValue(value);
            }

            Assert.Equal(["configured"], ((System.Collections.IEnumerable)value!).Cast<object>().Select(o => o.ToString()));
        }
    }

    private static IEnumerable<string> ListSettingsWithDefaults(Type type, object instance, string prefix)
    {
        foreach (var property in type.GetProperties())
        {
            var value = property.GetValue(instance);
            var path = prefix + property.Name;
            if (value is string || value == null || property.PropertyType.IsValueType)
            {
                continue;
            }

            if (value is System.Collections.IEnumerable list)
            {
                if (list.Cast<object>().Any())
                {
                    yield return path;
                }
            }
            else
            {
                foreach (var nested in ListSettingsWithDefaults(property.PropertyType, value, path + ":"))
                {
                    yield return nested;
                }
            }
        }
    }

    /// <summary>
    /// A plugin as built against ZauberCMS 4.x: a class implementing <c>IContentProperty</c> from ZauberCMS.Core,
    /// where v5 no longer has it (it moved to ZauberCMS.Components). Loaded beside the real Core, that class cannot
    /// load; the plugin's other public type can.
    /// </summary>
    private static Assembly EmitPluginBuiltAgainstZauber4(string name, bool publicEditor)
    {
        var stubCore = new PersistedAssemblyBuilder(
            new AssemblyName("ZauberCMS.Core") { Version = new Version(4, 0, 0, 0) }, typeof(object).Assembly);
        var movedInterface = stubCore.DefineDynamicModule("ZauberCMS.Core").DefineType(
            "ZauberCMS.Core.Content.Interfaces.IContentProperty",
            TypeAttributes.Public | TypeAttributes.Interface | TypeAttributes.Abstract);
        movedInterface.CreateType();

        var plugin = new PersistedAssemblyBuilder(new AssemblyName(name), typeof(object).Assembly);
        var module = plugin.DefineDynamicModule(name);
        var editor = module.DefineType(name + ".Editor",
            (publicEditor ? TypeAttributes.Public : TypeAttributes.NotPublic) | TypeAttributes.Class | TypeAttributes.Sealed,
            typeof(object), [movedInterface]);
        editor.DefineDefaultConstructor(MethodAttributes.Public);
        editor.CreateType();
        var loads = module.DefineType(name + ".Loads", TypeAttributes.Public | TypeAttributes.Class, typeof(object));
        loads.DefineDefaultConstructor(MethodAttributes.Public);
        loads.CreateType();

        using var image = new MemoryStream();
        plugin.Save(image);
        image.Position = 0;
        return new AssemblyLoadContext(name, isCollectible: true).LoadFromStream(image);
    }

    private static WebApplication BuildApp(IDictionary<string, string?>? extraSettings = null) =>
        TestSite.BuildApp("Sqlite", "DataSource=:memory:", TestSite.CreateContentRoot(), validateContainer: false,
            extraSettings);

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "ZauberCMS.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("ZauberCMS.sln not found above the test output.");
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

/// <summary>
/// An extension type discovery cannot see (it is not public): the bootstrap warns about it (ZA-120). It is in the
/// test assembly, which every test site scans, so it must do nothing.
/// </summary>
internal sealed class HiddenSeedData : ISeedData
{
    public void Initialise(IZauberDbContext dbContext)
    {
    }
}

/// <summary>
/// A FluentValidation validator nobody finds (it is not public): the bootstrap warns about it (ZA-182). In the test
/// assembly, which every test site scans, so it validates nothing.
/// </summary>
internal sealed class HiddenValidator : FluentValidation.AbstractValidator<HiddenSeedData>;

/// <summary>A start-up plugin the bootstrap cannot create (no parameterless constructor): skipped with a warning.</summary>
public sealed class StartupPluginWithoutDefaultConstructor(string name) : IStartupPlugin
{
    public string Name { get; } = name;

    public void Register(IServiceCollection services, IConfiguration configuration) =>
        throw new InvalidOperationException("Never created by the bootstrap.");
}
