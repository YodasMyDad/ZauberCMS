using System.Text.Json;
using ZauberCMS.Core.Content.Interfaces;

namespace ZauberCMS.Core.Content.Services;

/// <summary>
/// The built-in Block List editor stores its blocks as a JSON array of content ids, in order.
/// </summary>
public class BlockListNestedContentReferenceProvider : INestedContentReferenceProvider
{
    /// <summary>The Block List editor's alias (<c>BlockListEditorProperty.Alias</c> in ZauberCMS.Components).</summary>
    public const string BlockListEditorAlias = "ZauberCMS.BlockListEditor";

    public string EditorAlias => BlockListEditorAlias;

    public IReadOnlyList<Guid> GetReferencedIds(string? value)
    {
        var ids = Parse(value);
        return ids == null ? [] : ids.Where(id => id != Guid.Empty).Distinct().ToList();
    }

    public string? RemapIds(string? value, IReadOnlyDictionary<Guid, Guid> map)
    {
        var ids = Parse(value);
        if (ids == null) return value;
        return JsonSerializer.Serialize(ids.Where(id => id != Guid.Empty).Distinct()
            .Select(id => map.TryGetValue(id, out var mapped) ? mapped : id).ToList());
    }

    private static List<Guid>? Parse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.TrimStart()[0] != '[') return null;
        try
        {
            return JsonSerializer.Deserialize<List<Guid>>(value);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
