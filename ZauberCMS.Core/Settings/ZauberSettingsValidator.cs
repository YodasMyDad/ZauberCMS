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
        catch (CultureNotFoundException)
        {
            failures.Add($"Zauber:AdminDefaultLanguage is '{options.AdminDefaultLanguage}', which is not a culture " +
                         "name such as en-US.");
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
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

    private static void AddIfFailed(List<string> failures, string? failure)
    {
        if (failure != null)
        {
            failures.Add(failure);
        }
    }
}
