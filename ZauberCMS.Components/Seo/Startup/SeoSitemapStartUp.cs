using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ZauberCMS.Core.Plugins.Interfaces;

namespace ZauberCMS.Components.Seo.Startup;

public class SeoSitemapStartUp : IStartupPlugin
{
    public void Register(IServiceCollection services, IConfiguration configuration)
    {
        // Register the sitemap generator as a hosted service
        services.AddHostedService<SitemapGeneratorService>();
    }
}