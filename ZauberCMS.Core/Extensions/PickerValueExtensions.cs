using System.Text.Json;

namespace ZauberCMS.Core.Extensions;

/// <summary>
/// The stored value of a picker (content, media, users): a JSON array of ids in the order the editor chose, even for
/// one item (Roadmap D30). Older values that hold one bare id still read.
/// </summary>
public static class PickerValueExtensions
{
    /// <summary>
    /// The ids in a picker value, in stored order, without empty or repeated ids. Reads a JSON array of ids or one bare
    /// id; anything else gives an empty list.
    /// </summary>
    public static List<Guid> ToPickerIds(this string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        var trimmed = value.Trim();
        if (Guid.TryParse(trimmed, out var single))
        {
            return single == Guid.Empty ? [] : [single];
        }

        try
        {
            var ids = JsonSerializer.Deserialize<List<Guid>>(trimmed);
            return ids?.Where(id => id != Guid.Empty).Distinct().ToList() ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    /// <summary>The value a picker stores for <paramref name="ids"/>: a JSON array, or empty when there are none.</summary>
    public static string ToPickerValue(this IEnumerable<Guid> ids)
    {
        var list = ids.Where(id => id != Guid.Empty).Distinct().ToList();
        return list.Count == 0 ? string.Empty : JsonSerializer.Serialize(list);
    }

    /// <summary>
    /// <paramref name="items"/> in the order of <paramref name="ids"/>; ids with no item are left out. Queries return
    /// their own order, so a picker's items are put back in the order the editor chose.
    /// </summary>
    public static List<T> InPickerOrder<T>(this IEnumerable<T> items, IReadOnlyList<Guid> ids, Func<T, Guid> idOf)
    {
        var byId = new Dictionary<Guid, T>();
        foreach (var item in items)
        {
            byId.TryAdd(idOf(item), item);
        }

        var ordered = new List<T>(ids.Count);
        foreach (var id in ids)
        {
            if (byId.TryGetValue(id, out var item))
            {
                ordered.Add(item);
            }
        }

        return ordered;
    }
}
