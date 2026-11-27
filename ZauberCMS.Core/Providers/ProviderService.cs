using Microsoft.Extensions.Options;
using ZauberCMS.Core.Plugins;
using ZauberCMS.Core.Plugins.Interfaces;
using ZauberCMS.Core.Security.Interfaces;
using ZauberCMS.Core.Settings;

namespace ZauberCMS.Core.Providers;

public class ProviderService(IOptions<ZauberSettings> settings, ExtensionManager extensionManager)
{
    private readonly ZauberSettings _settings = settings.Value;

    private IStorageProvider? _storageProvider;

    /// <summary>
    /// The media storage chosen by <c>Zauber:Plugins:StorageProvider</c>, or <c>null</c> when that is blank.
    /// </summary>
    /// <exception cref="InvalidOperationException">The setting names no discovered provider (start-up already
    /// refuses that, ZA-120).</exception>
    public IStorageProvider? StorageProvider =>
        _storageProvider ??= Configured<IStorageProvider>(nameof(PluginSettings.StorageProvider),
            _settings.Plugins.StorageProvider);

    private IEmailProvider? _emailProvider;

    /// <summary>The email backend chosen by <c>Zauber:Plugins:EmailProvider</c>, or <c>null</c> when that is blank.</summary>
    /// <exception cref="InvalidOperationException">The setting names no discovered provider.</exception>
    public IEmailProvider? EmailProvider =>
        _emailProvider ??= Configured<IEmailProvider>(nameof(PluginSettings.EmailProvider),
            _settings.Plugins.EmailProvider);

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
