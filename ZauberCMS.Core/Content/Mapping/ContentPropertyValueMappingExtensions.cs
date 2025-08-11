using ZauberCMS.Core.Content.Models;

namespace ZauberCMS.Core.Content.Mapping;

/// <summary>
/// Extension methods for mapping ContentPropertyValue entities
/// Replaces AutoMapper ContentPropertyValueMapper profile
/// </summary>
public static class ContentPropertyValueMappingExtensions
{
    /// <summary>
    /// Maps properties from source ContentPropertyValue to target ContentPropertyValue, excluding navigation properties
    /// </summary>
    /// <param name="source">Source ContentPropertyValue entity</param>
    /// <param name="target">Target ContentPropertyValue entity to update</param>
    /// <returns>Updated target ContentPropertyValue entity</returns>
    public static ContentPropertyValue MapTo(this ContentPropertyValue source, ContentPropertyValue target)
    {
        // Map all simple properties
        target.Id = source.Id;
        target.ContentId = source.ContentId;
        target.Alias = source.Alias;
        target.ContentTypePropertyId = source.ContentTypePropertyId;
        target.Value = source.Value;
        target.DateCreated = source.DateCreated;
        target.DateUpdated = source.DateUpdated;

        // Excluded properties (navigation properties):
        // - Content (navigation property)

        return target;
    }

    /// <summary>
    /// Creates a new ContentPropertyValue entity with properties mapped from source
    /// </summary>
    /// <param name="source">Source ContentPropertyValue entity</param>
    /// <returns>New ContentPropertyValue entity with mapped properties</returns>
    public static ContentPropertyValue MapToNew(this ContentPropertyValue source)
    {
        var target = new ContentPropertyValue();
        return source.MapTo(target);
    }
}
