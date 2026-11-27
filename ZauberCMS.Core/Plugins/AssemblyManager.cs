using System.Collections.Concurrent;
using System.Reflection;

namespace ZauberCMS.Core.Plugins;

public static class AssemblyManager
{
    private static IEnumerable<Assembly?> _assemblies = null!;
#pragma warning disable CA2211 // public mutable statics, frozen by the public API until the v5 clean-up makes them internal (ZA-092)
    public static ConcurrentDictionary<Type, IEnumerable<Type>> Types = null!;
    public static ConcurrentDictionary<string, Type> TypesByName = null!;
#pragma warning restore CA2211

    /// <summary>
    /// Gets the cached assemblies that have been set by the SetAssemblies method.
    /// </summary>
    public static IEnumerable<Assembly?> Assemblies => _assemblies;

    /// <summary>
    /// Sets the assemblies and invalidates the type cache.
    /// </summary>
    /// <param name="assems">The assemblies to set.</param>
    public static void SetAssemblies(IEnumerable<Assembly?> assems)
    {
        _assemblies = assems;
        Types = new ConcurrentDictionary<Type, IEnumerable<Type>>();
        TypesByName = new ConcurrentDictionary<string, Type>();
    }
}