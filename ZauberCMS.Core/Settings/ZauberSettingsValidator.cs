using System.Globalization;
using Microsoft.Extensions.Options;
using ZauberCMS.Core.Content.Interfaces;
using ZauberCMS.Core.Membership.Interfaces;
using ZauberCMS.Core.Plugins;
using ZauberCMS.Core.Plugins.Interfaces;
using ZauberCMS.Core.Providers;
using ZauberCMS.Core.Security.Interfaces;

namespace ZauberCMS.Core.Settings;

/// <summary>
/// Checks <c>Zauber</c> settings that would otherwise fail later, at the first request that needs them, so the site
/// stops at start-up with the name of the setting instead (ZA-120, Roadmap D48). Registered with
/// <c>ValidateOnStart</c>; <c>AddZauberCms(app)</c> reads the settings first, so it fails there. The shipped
/// defaults pass, and so do empty database settings (the installer, D24).
/// </summary>
internal sealed class ZauberSettingsValidator(IServiceProvider services) : IValidateOptions<ZauberSettings>
{
    public ValidateOptionsResult Validate(string? name, ZauberSettings options)
    {
        var extensionManager = new ExtensionManager(services);
        var failures = new List<string>();

        AddIfFailed(failures, UnknownProvider<IStorageProvider>(extensionManager,
            nameof(PluginSettings.StorageProvider), options.Plugins.StorageProvider));
        AddIfFailed(failures, UnknownProvider<IEmailProvider>(extensionManager,
            nameof(PluginSettings.EmailProvider), options.Plugins.EmailProvider));
        AddIfFailed(failures, UnknownProvider<ICaptchaProvider>(extensionManager,
            nameof(PluginSettings.CaptchaProvider), options.Plugins.CaptchaProvider));

        // RequestLocalizationOptions.SetDefaultCulture fails on the same names, with no mention of the setting
        try
        {
            _ = CultureInfo.GetCultureInfo(options.AdminDefaultLanguage ?? string.Empty);
        }
        catch (CultureNotFoundException ex)
        {
            failures.Add(AdminDefaultLanguageFailure(options.AdminDefaultLanguage, ex, IsGlobalizationInvariant()));
        }

        failures.AddRange(ImageResizeFailures(options.ImageResize));

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }

    /// <summary>
    /// The <c>Zauber:ImageResize</c> numbers ImageResize refuses, each naming the setting, its value and what is valid
    /// (ZA-235, Roadmap D102). They are the rules of ImageResize's own options validator, which is internal to it and
    /// runs later, from <c>app.Run()</c> after the migrations, with ImageResize's option names; a test checks these are
    /// no looser than it.
    /// </summary>
    internal static IEnumerable<string> ImageResizeFailures(ImageResizeSettings settings)
    {
        if (settings.DefaultQuality is < 1 or > 100)
        {
            yield return ImageResizeFailure("DefaultQuality", settings.DefaultQuality,
                "use 1 to 100 (the quality of resized JPEG and WebP images)");
        }

        if (settings.PngCompressionLevel is < 0 or > 9)
        {
            yield return ImageResizeFailure("PngCompressionLevel", settings.PngCompressionLevel, "use 0 to 9");
        }

        if (settings.Cache.FolderSharding < 0)
        {
            yield return ImageResizeFailure("Cache:FolderSharding", settings.Cache.FolderSharding,
                "use 0 or more (the levels of folders the resized images are spread over)");
        }

        if (settings.Cache.MaxCacheBytes < 0)
        {
            yield return ImageResizeFailure("Cache:MaxCacheBytes", settings.Cache.MaxCacheBytes,
                "use 0 for no limit, or the most bytes the resized images may take");
        }

        if (settings.ResponseCache.ClientCacheSeconds < 0)
        {
            yield return ImageResizeFailure("ResponseCache:ClientCacheSeconds", settings.ResponseCache.ClientCacheSeconds,
                "use 0 or more seconds");
        }
    }

    private static string ImageResizeFailure(string setting, long value, string valid) =>
        string.Create(CultureInfo.InvariantCulture,
            $"Zauber:ImageResize:{setting} is {value}, which ImageResize does not accept: {valid}.");

    /// <summary>
    /// The error for an <c>AdminDefaultLanguage</c> the runtime cannot load, with the runtime's reason: in
    /// globalization-invariant mode a valid name fails too, as there are no cultures (ZA-179).
    /// </summary>
    internal static string AdminDefaultLanguageFailure(string? configured, CultureNotFoundException reason,
        bool globalizationInvariant)
    {
        var runtimeReason = string.Join(' ', reason.Message.Split(['\r', '\n'],
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        return $"Zauber:AdminDefaultLanguage is '{configured}', which the runtime cannot load as a culture: " +
               $"{runtimeReason} " + (globalizationInvariant
                   ? "The site runs in globalization-invariant mode (InvariantGlobalization in the project, or " +
                     "DOTNET_SYSTEM_GLOBALIZATION_INVARIANT, as some container images set), which has no cultures: " +
                     "turn it off (ZauberCMS needs ICU)."
                   : "Use a culture name such as en-US.");
    }

    // In invariant mode only the invariant culture exists, so even en-US cannot be loaded
    private static bool IsGlobalizationInvariant()
    {
        try
        {
            _ = CultureInfo.GetCultureInfo("en-US");
            return false;
        }
        catch (CultureNotFoundException)
        {
            return true;
        }
    }

    /// <summary>
    /// The error for a <c>Zauber:Plugins:*</c> setting that names a provider discovery did not find, or <c>null</c>
    /// when it names one or is blank (no provider). Lists the providers that were found.
    /// </summary>
    internal static string? UnknownProvider<T>(ExtensionManager extensionManager, string setting, string? configured)
    {
        if (string.IsNullOrWhiteSpace(configured))
        {
            return null;
        }

        var found = extensionManager.GetImplementations<T>(true);
        return found.ContainsKey(configured.Trim()) ? null : UnknownProviderMessage<T>(setting, configured, found.Keys);
    }

    internal static string UnknownProviderMessage<T>(string setting, string configured, IEnumerable<string> found)
    {
        var available = found.Order(StringComparer.Ordinal).ToList();
        return $"Zauber:Plugins:{setting} is '{configured}', which is not a public {typeof(T).Name} in the scanned " +
               "assemblies. " + (available.Count == 0
                   ? "None was found."
                   : "Found: " + string.Join(", ", available) + ".") +
               " Use the full type name (namespace and class), and pass the provider's assembly to AddZauberCms " +
               "if it is not the site's own.";
    }

    /// <summary>
    /// Settings that do not stop the site but do not do what they say: they are logged as warnings at every start.
    /// A view that is not found renders nothing, and a <c>SiteUrl</c> that is not an address is ignored, so links
    /// in emails are built from the request's Host header.
    /// </summary>
    internal static IEnumerable<string> FindWarnings(ZauberSettings settings, ExtensionManager extensionManager)
    {
        // A blank storage provider is a site that stores no files (D83): it starts, as it always has, but its media
        // section cannot upload. A blank email provider is a site that sends no email, as Umbraco runs without SMTP,
        // so it is not warned about: a send says what to set (ProviderService.GetRequiredEmailProvider).
        if (string.IsNullOrWhiteSpace(settings.Plugins.StorageProvider))
        {
            yield return "Zauber:Plugins:StorageProvider is empty, so this site has no media storage: uploads and " +
                         "permanent deletes of media files are refused. Set it to " +
                         "ZauberCMS.Core.Providers.DiskStorageProvider (the default) or to the full type name of an " +
                         "IStorageProvider of your own.";
        }

        if (!string.IsNullOrWhiteSpace(settings.SiteUrl) &&
            !(Uri.TryCreate(settings.SiteUrl, UriKind.Absolute, out var siteUri) &&
              (siteUri.Scheme == Uri.UriSchemeHttp || siteUri.Scheme == Uri.UriSchemeHttps)))
        {
            yield return $"Zauber:SiteUrl is '{settings.SiteUrl}', which is not an http or https address such as " +
                         "https://www.example.com. It is ignored: links in emails are built from the request's Host header.";
        }

        var views = extensionManager.GetImplementations<IContentView>(true);
        foreach (var (setting, value) in new[]
                 {
                     (nameof(ZauberSettings.NotFoundComponent), settings.NotFoundComponent),
                     (nameof(ZauberSettings.StarterComponent), settings.StarterComponent),
                     (nameof(ZauberSettings.MissingView), settings.MissingView)
                 })
        {
            if (!string.IsNullOrWhiteSpace(value) && !views.ContainsKey(value))
            {
                yield return $"Zauber:{setting} is '{value}', which is not a public IContentView in the scanned " +
                             "assemblies, so nothing is shown where it would be. Use the view's full type name.";
            }
        }

        var accountLayout = settings.Identity.AccountLayout;
        if (!string.IsNullOrWhiteSpace(accountLayout) &&
            !extensionManager.GetImplementations<IAccountLayout>(true).ContainsKey(accountLayout))
        {
            yield return $"Zauber:Identity:AccountLayout is '{accountLayout}', which is not a public IAccountLayout in " +
                         "the scanned assemblies, so the account pages use no layout. Use the layout's full type name.";
        }
    }

    /// <summary>
    /// The settings that decide which services <c>AddZauberCms(builder)</c> registers are read from configuration
    /// (D81), as Umbraco reads what it composes from <c>builder.Config</c>: the database (<c>DatabaseProvider</c>,
    /// <c>ConnectionString</c>, which the installer writes to configuration and the DbContexts read from it) and
    /// whether Redis is used, which is decided while the services are registered. What does not apply is a start-up
    /// warning: a database setting changed in code (<c>Configure&lt;ZauberSettings&gt;</c>, <c>PostConfigure</c>), and
    /// Redis set after the services were registered, in code or by a configuration source added after
    /// <c>AddZauberCms(builder)</c> (ZA-231). No value is written out: a connection string holds a password.
    /// </summary>
    /// <param name="composed">The settings as the bootstrap read them while it registered the services.</param>
    /// <param name="configured">The settings as configuration has them now, every source included.</param>
    /// <param name="options">The settings as <c>IOptions&lt;ZauberSettings&gt;</c> has them, code changes included.</param>
    internal static IEnumerable<string> FindChangesThatDoNotApply(ZauberSettings composed, ZauberSettings configured,
        ZauberSettings options)
    {
        const string setInConfiguration = "Set it in configuration instead (appsettings.json, environment " +
                                          "variables, user secrets or another configuration source).";
        if (!string.Equals(configured.DatabaseProvider?.Trim(), options.DatabaseProvider?.Trim(),
                StringComparison.OrdinalIgnoreCase))
        {
            yield return "Zauber:DatabaseProvider is changed in code (IOptions<ZauberSettings> differs from " +
                         "configuration), which does nothing: the database is read from configuration, where the " +
                         "installer writes it. " + setInConfiguration;
        }

        if (!string.Equals(configured.ConnectionString, options.ConnectionString, StringComparison.Ordinal))
        {
            yield return "Zauber:ConnectionString is changed in code (IOptions<ZauberSettings> differs from " +
                         "configuration), which does nothing: the database is read from configuration, where the " +
                         "installer writes it. " + setInConfiguration;
        }

        if (string.IsNullOrWhiteSpace(composed.RedisConnectionString) &&
            !string.IsNullOrWhiteSpace(options.RedisConnectionString))
        {
            yield return "Zauber:RedisConnectionString is set " + RedisChangedBy(configured, options) + ", which " +
                         "does nothing: whether Redis carries the cache clears between servers is decided when " +
                         "AddZauberCms(builder) registers the services, so this site runs without Redis. Set it in " +
                         "configuration, with every configuration source added before builder.AddZauberCms().";
        }
    }

    /// <summary>
    /// A change that would leave registered services unable to work, or <c>null</c>: Redis set when the services
    /// were registered (D81) but emptied afterwards, in code or by a configuration source added after
    /// <c>AddZauberCms(builder)</c>. The Redis services would have no address, so the start stops naming the setting
    /// (D48).
    /// </summary>
    internal static string? ChangeThatCannotWork(ZauberSettings composed, ZauberSettings configured,
        ZauberSettings options) =>
        !string.IsNullOrWhiteSpace(composed.RedisConnectionString) &&
        string.IsNullOrWhiteSpace(options.RedisConnectionString)
            ? "Zauber:RedisConnectionString was set when AddZauberCms(builder) registered the services, so the Redis " +
              "services are registered, but it is emptied " + RedisChangedBy(configured, options) + ": they would " +
              "have no address. To run without Redis, remove it from configuration, with every configuration source " +
              "added before builder.AddZauberCms()."
            : null;

    // Where a Redis change made after the services were registered came from: code, when the options differ from
    // configuration, else a configuration source added after AddZauberCms(builder)
    private static string RedisChangedBy(ZauberSettings configured, ZauberSettings options) =>
        string.Equals(configured.RedisConnectionString, options.RedisConnectionString, StringComparison.Ordinal)
            ? "by a configuration source added after AddZauberCms(builder)"
            : "in code (Configure<ZauberSettings>)";

    private static void AddIfFailed(List<string> failures, string? failure)
    {
        if (failure != null)
        {
            failures.Add(failure);
        }
    }
}
