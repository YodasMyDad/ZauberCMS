using System.Reflection;

namespace ZauberCMS.Core.Plugins;

/// <summary>
/// The assemblies ZauberCMS scans for extension types, set once by the bootstrap. Internal since v5 (ZA-092): a site
/// adds assemblies through <c>AddZauberCms(params Assembly[])</c>.
/// </summary>
internal static class AssemblyManager
{
    private static IEnumerable<Assembly?> _assemblies = [];

    /// <summary>
    /// Gets the cached assemblies that have been set by the SetAssemblies method (none until then).
    /// </summary>
    public static IEnumerable<Assembly?> Assemblies => _assemblies;

    /// <summary>
    /// Sets the assemblies and invalidates the type caches, <see cref="ExtensionManager"/>'s included (ZA-120).
    /// </summary>
    /// <param name="assems">The assemblies to set.</param>
    public static void SetAssemblies(IEnumerable<Assembly?> assems)
    {
        _assemblies = assems;
        ExtensionManager.ClearCaches();
    }
}
