using Microsoft.Extensions.Options;
using ZauberCMS.Core.Plugins;
using ZauberCMS.Core.Plugins.Interfaces;
using ZauberCMS.Core.Settings;

namespace ZauberCMS.Core.Providers;

public class ProviderService(IOptions<ZauberSettings> gabSettings, ExtensionManager extensionManager)
{
    private readonly ZauberSettings _settings = gabSettings.Value;

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
}