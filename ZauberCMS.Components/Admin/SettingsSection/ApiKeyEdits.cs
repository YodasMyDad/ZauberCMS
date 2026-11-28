using Microsoft.Extensions.Localization;
using ZauberCMS.Core.Localization;

namespace ZauberCMS.Components.Admin.SettingsSection;

/// <summary>
/// The API keys being edited on the Global Settings page (ZA-214, Roadmap D73). The saved values stay in here, on
/// the server, and are never put in a box: each row starts with an empty value, a typed value replaces the saved
/// one, an empty box keeps it, and a renamed row keeps the value it was saved with.
/// </summary>
internal sealed class ApiKeyEdits
{
    private Dictionary<string, string> _saved = [];

    public ApiKeyEdits(IReadOnlyDictionary<string, string> saved) => Load(saved);

    /// <summary>What the page shows: names, and only values typed since the keys were last saved.</summary>
    public List<ApiKeyRow> Rows { get; private set; } = [];

    /// <summary>Starts again from <paramref name="saved"/>, as after a save: every value box empty.</summary>
    public void Load(IReadOnlyDictionary<string, string> saved)
    {
        _saved = saved.ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal);
        Rows = _saved.Keys.Order(StringComparer.OrdinalIgnoreCase)
            .Select(name => new ApiKeyRow { Name = name, SavedName = name })
            .ToList();
    }

    /// <summary>Adds a key; null when added, else why not, in <paramref name="text"/>'s language.</summary>
    public string? Add(string? name, string? value, IStringLocalizer<ZauberText> text)
    {
        name = name?.Trim();
        if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(value))
        {
            return text["settings_apiKeyNameAndValueRequired"].Value;
        }

        if (Rows.Any(x => string.Equals(x.Name?.Trim(), name, StringComparison.OrdinalIgnoreCase)))
        {
            return text["settings_apiKeyNameTaken", name].Value;
        }

        Rows.Add(new ApiKeyRow { Name = name, NewValue = value });
        return null;
    }

    /// <summary>The keys to save, or why they cannot be: a key without a name or a value, or two of one name
    /// (in <paramref name="text"/>'s language).</summary>
    public (Dictionary<string, string>? Keys, string? Problem) ToSave(IStringLocalizer<ZauberText> text)
    {
        var keys = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in Rows)
        {
            var name = row.Name?.Trim();
            var value = !string.IsNullOrEmpty(row.NewValue)
                ? row.NewValue
                : row.SavedName != null ? _saved.GetValueOrDefault(row.SavedName) : null;
            if (string.IsNullOrEmpty(name))
            {
                return (null, text["settings_apiKeyNameRequired"].Value);
            }

            if (string.IsNullOrEmpty(value))
            {
                return (null, text["settings_apiKeyValueRequired", name].Value);
            }

            if (!keys.TryAdd(name, value))
            {
                return (null, text["settings_apiKeyNameTwice", name].Value);
            }
        }

        return (keys, null);
    }
}

/// <summary>One API key on the Global Settings page.</summary>
internal sealed class ApiKeyRow
{
    public string? Name { get; set; }

    /// <summary>The name its value is saved under; null for a key added since the keys were last saved.</summary>
    public string? SavedName { get; init; }

    /// <summary>A value typed to replace the saved one; empty keeps it.</summary>
    public string? NewValue { get; set; }
}
