using System.Collections;
using System.Collections.Concurrent;
using System.Globalization;
using System.Reflection;
using System.Resources;
using Microsoft.Extensions.Logging;

namespace ZauberCMS.Core.Localization;

/// <summary>
/// Where ZauberCMS's text comes from: one table of keys per culture from each layer, looked up most specific
/// culture first and, at each culture, in layer order (Roadmap D90). Each table is read once and kept.
/// </summary>
internal sealed class ZauberTextCatalog
{
    private readonly IReadOnlyList<IZauberTextLayer> _layers;
    private readonly ConcurrentDictionary<(int Layer, string Culture), IReadOnlyDictionary<string, string>> _tables = new();

    /// <param name="layers">The sources of text, the one that wins first.</param>
    public ZauberTextCatalog(IReadOnlyList<IZauberTextLayer> layers)
    {
        _layers = layers;
    }

    /// <summary>
    /// The layers of a site: the site and plugin assemblies in the order they are scanned (the site's entry
    /// assembly first, then the ones passed to <c>AddZauberCms</c>), then ZauberCMS's own, whose English is the base.
    /// </summary>
    public static ZauberTextCatalog FromAssemblies(IEnumerable<Assembly> scannedAssemblies,
        IEnumerable<Assembly> zauberAssemblies, ILoggerFactory loggerFactory)
    {
        var logger = loggerFactory.CreateLogger<ZauberTextCatalog>();
        var own = zauberAssemblies.Distinct().ToList();
        var layers = scannedAssemblies.Distinct()
            .Where(a => !own.Contains(a))
            .Concat(own)
            .Select(IZauberTextLayer (a) => new AssemblyTextLayer(a, logger))
            .ToList();
        return new ZauberTextCatalog(layers);
    }

    /// <summary>The text of <paramref name="key"/> in <paramref name="culture"/>, or null when no layer has it.</summary>
    public string? Find(string key, CultureInfo culture)
    {
        foreach (var step in CultureChain(culture))
        {
            for (var layer = 0; layer < _layers.Count; layer++)
            {
                if (Table(layer, step).TryGetValue(key, out var value))
                {
                    return value;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Every key with the text it shows in <paramref name="culture"/>; with <paramref name="includeParentCultures"/>
    /// false, only the keys translated into that culture itself.
    /// </summary>
    public IReadOnlyDictionary<string, string> All(CultureInfo culture, bool includeParentCultures)
    {
        var all = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var step in includeParentCultures ? CultureChain(culture) : [culture])
        {
            for (var layer = 0; layer < _layers.Count; layer++)
            {
                foreach (var (key, value) in Table(layer, step))
                {
                    all.TryAdd(key, value);
                }
            }
        }

        return all;
    }

    // fr-CA, fr, then the neutral files (the invariant culture)
    private static IEnumerable<CultureInfo> CultureChain(CultureInfo culture)
    {
        for (var step = culture; ; step = step.Parent)
        {
            yield return step;
            if (step.Name.Length == 0)
            {
                yield break;
            }
        }
    }

    private IReadOnlyDictionary<string, string> Table(int layer, CultureInfo culture) =>
        _tables.GetOrAdd((layer, culture.Name), static (key, state) => state.Layers[key.Layer].Load(state.Culture),
            (Layers: _layers, Culture: culture));
}

/// <summary>A source of ZauberCMS text: the keys it has for one culture (the invariant culture: its neutral files).</summary>
internal interface IZauberTextLayer
{
    IReadOnlyDictionary<string, string> Load(CultureInfo culture);
}

/// <summary>
/// The <c>.resx</c> files of one assembly in a <see cref="ZauberText.ResourceFolder"/> folder: the neutral ones
/// in the assembly itself, a culture's in its satellite assembly (<c>fr/MySite.resources.dll</c>).
/// </summary>
internal sealed class AssemblyTextLayer(Assembly assembly, ILogger logger) : IZauberTextLayer
{
    private static readonly IReadOnlyDictionary<string, string> Empty = new Dictionary<string, string>();
    private const string Folder = ZauberText.ResourceFolder + ".";

    public IReadOnlyDictionary<string, string> Load(CultureInfo culture)
    {
        if (assembly.IsDynamic)
        {
            return Empty;
        }

        var source = assembly;
        if (culture.Name.Length != 0)
        {
            try
            {
                source = assembly.GetSatelliteAssembly(culture);
            }
            catch (FileNotFoundException)
            {
                return Empty;
            }
            catch (FileLoadException ex)
            {
                logger.LogWarning(ex, "The {Culture} resources of {Assembly} could not be loaded, so its ZauberCMS " +
                                      "text in that language is not used", culture.Name, assembly.GetName().Name);
                return Empty;
            }
        }

        // MySite.Resources.ZauberText.General.resources, MySite.Resources.ZauberText.General.fr.resources
        var suffix = culture.Name.Length == 0 ? ".resources" : "." + culture.Name + ".resources";
        var table = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var name in source.GetManifestResourceNames())
        {
            if (!name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase) ||
                !(name.StartsWith(Folder, StringComparison.Ordinal) || name.Contains("." + Folder, StringComparison.Ordinal)))
            {
                continue;
            }

            using var stream = source.GetManifestResourceStream(name);
            if (stream is null)
            {
                continue;
            }

            using var reader = new ResourceReader(stream);
            var entries = reader.GetEnumerator();
            while (entries.MoveNext())
            {
                if (entries.Key is not string key || StringValue(entries) is not { } value)
                {
                    continue;
                }

                if (!table.TryAdd(key, value))
                {
                    logger.LogWarning("The text {Key} is in more than one {Folder} file of {Assembly} ({Culture}); " +
                                      "the first one read is used", key, ZauberText.ResourceFolder,
                        assembly.GetName().Name, culture.Name.Length == 0 ? "neutral" : culture.Name);
                }
            }
        }

        return table;
    }

    // A resource that is not a string (an image in a text file) is not text: skip it
    private static string? StringValue(IDictionaryEnumerator entries)
    {
        try
        {
            return entries.Value as string;
        }
        catch (NotSupportedException)
        {
            return null;
        }
    }
}
