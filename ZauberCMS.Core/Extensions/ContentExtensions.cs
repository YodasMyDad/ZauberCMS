namespace ZauberCMS.Core.Extensions;

public static class ContentExtensions
{
    public static T? GetValue<T>(this Content.Models.Content content, string alias)
    {
        return content.ContentValues.TryGetValue(alias, out var contentValue) ? contentValue.Value.ToValue<T>() : default;
    }
}