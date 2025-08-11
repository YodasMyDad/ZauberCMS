using ZauberCMS.Core.Languages.Models;

namespace ZauberCMS.Core.Languages.Mapping;

/// <summary>
/// Extension methods for mapping LanguageDictionary entities
/// Replaces AutoMapper LanguageDictionaryMapper profile
/// </summary>
public static class LanguageDictionaryMappingExtensions
{
    /// <summary>
    /// Maps properties from source LanguageDictionary to target LanguageDictionary, excluding navigation properties
    /// </summary>
    /// <param name="source">Source LanguageDictionary entity</param>
    /// <param name="target">Target LanguageDictionary entity to update</param>
    /// <returns>Updated target LanguageDictionary entity</returns>
    public static LanguageDictionary MapTo(this LanguageDictionary source, LanguageDictionary target)
    {
        // Map all simple properties
        target.Id = source.Id;
        target.Key = source.Key;

        // Excluded properties (navigation properties):
        // - Texts (navigation property)

        return target;
    }

    /// <summary>
    /// Creates a new LanguageDictionary entity with properties mapped from source
    /// </summary>
    /// <param name="source">Source LanguageDictionary entity</param>
    /// <returns>New LanguageDictionary entity with mapped properties</returns>
    public static LanguageDictionary MapToNew(this LanguageDictionary source)
    {
        var target = new LanguageDictionary();
        return source.MapTo(target);
    }
}
