using ZauberCMS.Core.Membership.Models;

namespace ZauberCMS.Core.Membership.Mapping;

/// <summary>
/// Extension methods for mapping Role entities
/// Replaces AutoMapper RoleMapper profile
/// </summary>
public static class RoleMappingExtensions
{
    /// <summary>
    /// Maps properties from source Role to target Role, excluding navigation properties
    /// </summary>
    /// <param name="source">Source Role entity</param>
    /// <param name="target">Target Role entity to update</param>
    /// <returns>Updated target Role entity</returns>
    public static Role MapTo(this Role source, Role target)
    {
        // Map all simple properties from IdentityRole
        target.Id = source.Id;
        target.Name = source.Name;
        target.NormalizedName = source.NormalizedName;
        target.ConcurrencyStamp = source.ConcurrencyStamp;

        // Map custom properties
        target.Description = source.Description;
        target.Icon = source.Icon;
        target.DateCreated = source.DateCreated;
        target.DateUpdated = source.DateUpdated;
        target.ExtendedData = source.ExtendedData;
        target.Properties = source.Properties;
        target.Tabs = source.Tabs;

        // Excluded properties (navigation properties):
        // - UserRoles (navigation property)
        // - ContentRoles (navigation property)
        // - MediaRoles (navigation property)

        return target;
    }

    /// <summary>
    /// Creates a new Role entity with properties mapped from source
    /// </summary>
    /// <param name="source">Source Role entity</param>
    /// <returns>New Role entity with mapped properties</returns>
    public static Role MapToNew(this Role source)
    {
        var target = new Role();
        return source.MapTo(target);
    }
}
