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

    public IStorageProvider? StorageProvider
    {
        get
        {
            if (_storageProvider == null)
            {
                var storageProviders = extensionManager.GetInstances<IStorageProvider>(true);
                if (_settings.Plugins.StorageProvider != null)
                {
                    _storageProvider = storageProviders[_settings.Plugins.StorageProvider];
                }
            }
            return _storageProvider;
        }
    }

    private IEmailProvider? _emailProvider;
    
    public IEmailProvider? EmailProvider
    {
        get
        {
            if (_emailProvider == null)
            {
                var emailProviders = extensionManager.GetInstances<IEmailProvider>(true);
                if (_settings.Plugins.EmailProvider != null)
                {
                    _emailProvider = emailProviders[_settings.Plugins.EmailProvider];
                }
            }
            return _emailProvider;
        }
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
                    throw new InvalidOperationException(
                        $"Zauber:Plugins:CaptchaProvider names '{configured}', which is not a public ICaptchaProvider " +
                        "in the scanned assemblies.");
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
