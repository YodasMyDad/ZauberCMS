using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;

namespace ZauberCMS.Core.Localization;

internal static class ZauberTextServiceCollectionExtensions
{
    /// <summary>
    /// .NET localisation for the site (resources under <c>Resources</c>) and ZauberCMS's own text,
    /// <c>IStringLocalizer&lt;ZauberText&gt;</c>, from the <see cref="ZauberText.ResourceFolder"/> files of the
    /// scanned assemblies (Roadmap D90). Tried, so a site that registers its own localiser first keeps it; one
    /// registered after <c>AddZauberCms</c> replaces it.
    /// </summary>
    /// <param name="scannedAssemblies">Every assembly discovery scans, in its order.</param>
    /// <param name="zauberAssemblies">ZauberCMS's own: their English is the base under the others.</param>
    internal static IServiceCollection AddZauberText(this IServiceCollection services,
        IEnumerable<Assembly> scannedAssemblies, IEnumerable<Assembly> zauberAssemblies)
    {
        // Copied now: the caller's list is not read again after the start-up
        var scanned = scannedAssemblies.ToArray();
        var own = zauberAssemblies.ToArray();

        services.AddLocalization(options => options.ResourcesPath = "Resources");
        services.TryAddSingleton(sp =>
            ZauberTextCatalog.FromAssemblies(scanned, own, sp.GetRequiredService<ILoggerFactory>()));
        services.TryAddSingleton<IStringLocalizer<ZauberText>, ZauberTextLocalizer>();
        return services;
    }
}
