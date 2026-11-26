using System.Text.Json;
using ZauberCMS.Core.Tags.Interfaces;

namespace ZauberCMS.Core.Tags.Services;

/// <summary>Reads the built-in tags editor (<c>ZauberCMS.Tags</c>), which stores a JSON array of tag ids.</summary>
public class TagsPropertyTagReferenceProvider : ITagReferenceProvider
{
    public string EditorAlias => "ZauberCMS.Tags";

    public IReadOnlyCollection<Guid> GetTagIds(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return [];
        try
        {
            return JsonSerializer.Deserialize<List<Guid>>(value) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }
}
