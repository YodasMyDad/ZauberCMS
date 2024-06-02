using ZauberCMS.Core.Content.Models;

namespace ZauberCMS.Core.Extensions;

public static class ContentExtensions
{
    /// <summary>
    /// Get a value from a content property
    /// </summary>
    /// <param name="content">Content item</param>
    /// <param name="alias">The property alias</param>
    /// <typeparam name="T">The type you expect the value in</typeparam>
    /// <returns></returns>
    public static T? GetValue<T>(this Content.Models.Content content, string alias)
    {
        return content.ContentValues.TryGetValue(alias, out var contentValue) ? contentValue.Value.ToValue<T>() : default;
    }

    /// <summary>
    /// Gets a bit of content ready to be saved in the block list
    /// </summary>
    /// <param name="content"></param>
    /// <param name="contentType"></param>
    public static void ToBlockListItem(this Content.Models.Content? content, ContentType? contentType)
    {
        if (content != null && contentType != null)
        {
            content.ContentType = null;
            content.ContentTypeAlias = contentType.Alias;   
        }
    }

    private static readonly SlugHelper SlugHelper = new(new SlugHelper.Config
    {
        CharacterReplacements = new Dictionary<string, string> {{" ", ""}}
    });
    
    /// <summary>
    /// Content alias creator
    /// </summary>
    /// <param name="name"></param>
    /// <returns></returns>
    public static string ToAlias(this string? name)
    {
        return name != null ? SlugHelper.GenerateSlug(name) : string.Empty;
    }
}