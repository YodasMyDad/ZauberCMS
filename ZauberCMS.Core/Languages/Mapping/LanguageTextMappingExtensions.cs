using ZauberCMS.Core.Languages.Models;

namespace ZauberCMS.Core.Languages.Mapping;

/// <summary>
/// Extension methods for mapping LanguageText entities
/// Replaces AutoMapper LanguageTextMapper profile
/// </summary>
public static class LanguageTextMappingExtensions
{
    /// <summary>
    /// Maps properties from source LanguageText to target LanguageText, excluding navigation properties
    /// </summary>
    /// <param name="source">Source LanguageText entity</param>
    /// <param name="target">Target LanguageText entity to update</param>
    /// <returns>Updated target LanguageText entity</returns>
    public static LanguageText MapTo(this LanguageText source, LanguageText target)
    {
        // Map all simple properties
        target.Id = source.Id;
        target.LanguageDictionaryId = source.LanguageDictionaryId;
        target.LanguageId = source.LanguageId;
        target.Value = source.Value;

        // Excluded properties (navigation properties):
        // - LanguageDictionary (navigation property)
        // - Language (navigation property)

        return target;
    }

    /// <summary>
    /// Creates a new LanguageText entity with properties mapped from source
    /// </summary>
    /// <param name="source">Source LanguageText entity</param>
    /// <returns>New LanguageText entity with mapped properties</returns>
    public static LanguageText MapToNew(this LanguageText source)
    {
        var target = new LanguageText();
        return source.MapTo(target);
    }
}
