using System.Collections.Concurrent;
using System.Globalization;
using Microsoft.Extensions.Localization;
using Radzen;
using ZauberCMS.Core.Localization;

namespace ZauberCMS.Components.Startup;

/// <summary>
/// Radzen's own text (a data grid's "No records to display.", its filter and pager, dialogs, date pickers) from
/// ZauberCMS's text: Radzen's key <c>DataGrid_EmptyText</c> is <c>radzen_DataGrid_EmptyText</c> in a
/// <c>Radzen.fr.resx</c> of a site, plugin or language pack (Roadmap D93). A key no file has falls back to Radzen's
/// built-in English, so ZauberCMS ships no Radzen file.
/// </summary>
internal sealed class RadzenTextLocalizer(IStringLocalizer<ZauberText> text) : ILocalizer
{
    /// <summary>The area of Radzen's keys; the rest of the key is Radzen's own, as in its <c>RadzenStrings</c>.</summary>
    internal const string Prefix = "radzen_";

    // Radzen asks on every render, so each culture's Radzen keys are read once. The texts do not change while
    // the site runs (resources are compiled in).
    private readonly ConcurrentDictionary<string, Dictionary<string, string>> _cultures = new(StringComparer.Ordinal);

    public string? Get(string key, CultureInfo culture) =>
        _cultures.GetOrAdd(culture.Name, static (_, state) => Read(state.Text, state.Culture), (Text: text, Culture: culture))
            .TryGetValue(key, out var value) ? value : null;

    // Every key that is there, so a key nobody translated is not looked up (and not logged as missing) each time
    private static Dictionary<string, string> Read(IStringLocalizer text, CultureInfo culture)
    {
        var previous = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = culture;
            return text.GetAllStrings(includeParentCultures: true)
                .Where(s => s.Name.StartsWith(Prefix, StringComparison.Ordinal))
                .ToDictionary(s => s.Name[Prefix.Length..], s => s.Value, StringComparer.Ordinal);
        }
        finally
        {
            CultureInfo.CurrentUICulture = previous;
        }
    }
}
