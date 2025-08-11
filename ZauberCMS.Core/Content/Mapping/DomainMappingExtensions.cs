using ZauberCMS.Core.Content.Models;

namespace ZauberCMS.Core.Content.Mapping;

/// <summary>
/// Extension methods for mapping Domain entities
/// Replaces AutoMapper DomainMapper profile
/// </summary>
public static class DomainMappingExtensions
{
    /// <summary>
    /// Maps properties from source Domain to target Domain, excluding navigation properties
    /// </summary>
    /// <param name="source">Source Domain entity</param>
    /// <param name="target">Target Domain entity to update</param>
    /// <returns>Updated target Domain entity</returns>
    public static Domain MapTo(this Domain source, Domain target)
    {
        // Map all simple properties
        target.Id = source.Id;
        target.ContentId = source.ContentId;
        target.Url = source.Url;
        target.LanguageId = source.LanguageId;
        target.DateCreated = source.DateCreated;
        target.DateUpdated = source.DateUpdated;

        // Excluded properties (navigation properties):
        // - Language (navigation property)
        // - Redirects (navigation property)

        return target;
    }

    /// <summary>
    /// Creates a new Domain entity with properties mapped from source
    /// </summary>
    /// <param name="source">Source Domain entity</param>
    /// <returns>New Domain entity with mapped properties</returns>
    public static Domain MapToNew(this Domain source)
    {
        var target = new Domain();
        return source.MapTo(target);
    }
}
