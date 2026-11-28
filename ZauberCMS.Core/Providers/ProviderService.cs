using Microsoft.Extensions.Options;
using ZauberCMS.Core.Plugins;
using ZauberCMS.Core.Plugins.Interfaces;
using ZauberCMS.Core.Security.Interfaces;
using ZauberCMS.Core.Settings;

namespace ZauberCMS.Core.Providers;

public class ProviderService(IOptions<ZauberSettings> settings, ExtensionManager extensionManager)
{
    private readonly ZauberSettings _settings = settings.Value;

    /// <summary>
    /// Why a file cannot be stored or removed on a site whose <c>Zauber:Plugins:StorageProvider</c> is blank
    /// (ZA-196, Roadmap D83).
    /// </summary>
    public const string NoStorageProviderMessage =
        "Media storage is not set up: Zauber:Plugins:StorageProvider is empty, so files cannot be stored or removed. " +
        "Set it to ZauberCMS.Core.Providers.DiskStorageProvider (the default) or to the full type name of an " +
        "IStorageProvider of your own.";

    /// <summary>
    /// Why an email cannot be sent on a site whose <c>Zauber:Plugins:EmailProvider</c> is blank: such a site sends no
    /// email (ZA-196, Roadmap D83), as an SMTP provider without <c>Zauber:Email:Smtp:Host</c> sends none.
    /// </summary>
    public const string NoEmailProviderMessage =
        "Email is not set up: Zauber:Plugins:EmailProvider is empty, so no email can be sent. Set it to " +
        "ZauberCMS.Core.Providers.SmtpEmailProvider (the default, with Zauber:Email) or to the full type name of an " +
        "IEmailProvider of your own.";

    private IStorageProvider? _storageProvider;

    /// <summary>
    /// The media storage chosen by <c>Zauber:Plugins:StorageProvider</c>, or <c>null</c> when that is blank (a site
    /// that stores no files; start-up warns about it). Code that stores or removes a file calls
    /// <see cref="GetRequiredStorageProvider"/>, which says what to set.
    /// </summary>
    /// <exception cref="InvalidOperationException">The setting names no discovered provider (start-up already
    /// refuses that, ZA-120).</exception>
    public IStorageProvider? StorageProvider =>
        _storageProvider ??= Configured<IStorageProvider>(nameof(PluginSettings.StorageProvider),
            _settings.Plugins.StorageProvider);

    /// <summary>The media storage chosen by <c>Zauber:Plugins:StorageProvider</c>.</summary>
    /// <exception cref="InvalidOperationException">The setting is blank (<see cref="NoStorageProviderMessage"/>), or
    /// names no discovered provider.</exception>
    public IStorageProvider GetRequiredStorageProvider() =>
        StorageProvider ?? throw new InvalidOperationException(NoStorageProviderMessage);

    private IEmailProvider? _emailProvider;

    /// <summary>
    /// The email backend chosen by <c>Zauber:Plugins:EmailProvider</c>, or <c>null</c> when that is blank (a site that
    /// sends no email). Code that sends one calls <see cref="GetRequiredEmailProvider"/>, which says what to set.
    /// </summary>
    /// <exception cref="InvalidOperationException">The setting names no discovered provider.</exception>
    public IEmailProvider? EmailProvider =>
        _emailProvider ??= Configured<IEmailProvider>(nameof(PluginSettings.EmailProvider),
            _settings.Plugins.EmailProvider);

    /// <summary>The email backend chosen by <c>Zauber:Plugins:EmailProvider</c>.</summary>
    /// <exception cref="InvalidOperationException">The setting is blank (<see cref="NoEmailProviderMessage"/>), or
    /// names no discovered provider.</exception>
    public IEmailProvider GetRequiredEmailProvider() =>
        EmailProvider ?? throw new InvalidOperationException(NoEmailProviderMessage);

    private T? Configured<T>(string setting, string? configured) where T : class
    {
        if (string.IsNullOrWhiteSpace(configured))
        {
            return null;
        }

        var providers = extensionManager.GetInstances<T>(true);
        return providers.TryGetValue(configured.Trim(), out var provider)
            ? provider
            : throw new InvalidOperationException(
                ZauberSettingsValidator.UnknownProviderMessage<T>(setting, configured, providers.Keys));
    }

    private ICaptchaProvider? _captchaProvider;
    private bool _captchaProviderResolved;

    /// <summary>
    /// The CAPTCHA the registration page uses, or <c>null</c> when the site has none. Chosen by
    /// <c>Zauber:Plugins:CaptchaProvider</c>; when that is empty, the first discovered provider by type name.
    /// </summary>
    /// <exception cref="InvalidOperationException">The configured type is not a discovered provider. A site
    /// that asked for a CAPTCHA must not run without one.</exception>
    public ICaptchaProvider? CaptchaProvider
    {
        get
        {
            if (_captchaProviderResolved)
            {
                return _captchaProvider;
            }

            var captchaProviders = extensionManager.GetInstances<ICaptchaProvider>(true);
            var configured = _settings.Plugins.CaptchaProvider;
            if (!string.IsNullOrWhiteSpace(configured))
            {
                if (!captchaProviders.TryGetValue(configured.Trim(), out _captchaProvider))
                {
                    throw new InvalidOperationException(ZauberSettingsValidator.UnknownProviderMessage<ICaptchaProvider>(
                        nameof(PluginSettings.CaptchaProvider), configured, captchaProviders.Keys));
                }
            }
            else
            {
                _captchaProvider = captchaProviders
                    .OrderBy(x => x.Key, StringComparer.Ordinal)
                    .Select(x => x.Value)
                    .FirstOrDefault();
            }

            _captchaProviderResolved = true;
            return _captchaProvider;
        }
    }
}
