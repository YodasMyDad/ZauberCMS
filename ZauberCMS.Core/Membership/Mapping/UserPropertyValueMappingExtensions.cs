using ZauberCMS.Core.Membership.Models;

namespace ZauberCMS.Core.Membership.Mapping;

/// <summary>
/// Extension methods for mapping UserPropertyValue entities
/// Replaces AutoMapper UserPropertyValueMapper profile
/// </summary>
public static class UserPropertyValueMappingExtensions
{
    /// <summary>
    /// Maps properties from source UserPropertyValue to target UserPropertyValue, excluding navigation properties
    /// </summary>
    /// <param name="source">Source UserPropertyValue entity</param>
    /// <param name="target">Target UserPropertyValue entity to update</param>
    /// <returns>Updated target UserPropertyValue entity</returns>
    public static UserPropertyValue MapTo(this UserPropertyValue source, UserPropertyValue target)
    {
        // Map all simple properties
        target.Id = source.Id;
        target.Alias = source.Alias;
        target.ContentTypePropertyId = source.ContentTypePropertyId;
        target.Value = source.Value;
        target.DateUpdated = source.DateUpdated;
        target.UserId = source.UserId;

        // Excluded properties (navigation properties):
        // - User (navigation property)

        return target;
    }

    /// <summary>
    /// Creates a new UserPropertyValue entity with properties mapped from source
    /// </summary>
    /// <param name="source">Source UserPropertyValue entity</param>
    /// <returns>New UserPropertyValue entity with mapped properties</returns>
    public static UserPropertyValue MapToNew(this UserPropertyValue source)
    {
        var target = new UserPropertyValue();
        return source.MapTo(target);
    }
}
