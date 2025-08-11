using ZauberCMS.Core.Membership.Models;

namespace ZauberCMS.Core.Membership.Mapping;

/// <summary>
/// Extension methods for mapping User entities
/// Replaces AutoMapper UserMapper profile
/// </summary>
public static class UserMappingExtensions
{
    /// <summary>
    /// Maps properties from source User to target User, excluding navigation properties
    /// </summary>
    /// <param name="source">Source User entity</param>
    /// <param name="target">Target User entity to update</param>
    /// <returns>Updated target User entity</returns>
    public static User MapTo(this User source, User target)
    {
        // Map all simple properties from IdentityUser
        target.Id = source.Id;
        target.UserName = source.UserName;
        target.NormalizedUserName = source.NormalizedUserName;
        target.Email = source.Email;
        target.NormalizedEmail = source.NormalizedEmail;
        target.EmailConfirmed = source.EmailConfirmed;
        target.PasswordHash = source.PasswordHash;
        target.SecurityStamp = source.SecurityStamp;
        target.ConcurrencyStamp = source.ConcurrencyStamp;
        target.PhoneNumber = source.PhoneNumber;
        target.PhoneNumberConfirmed = source.PhoneNumberConfirmed;
        target.TwoFactorEnabled = source.TwoFactorEnabled;
        target.LockoutEnd = source.LockoutEnd;
        target.LockoutEnabled = source.LockoutEnabled;
        target.AccessFailedCount = source.AccessFailedCount;

        // Map custom properties
        target.DateCreated = source.DateCreated;
        target.DateUpdated = source.DateUpdated;
        target.ExtendedData = source.ExtendedData;

        // Excluded properties (navigation properties):
        // - UserRoles (navigation property)
        // - Audits (navigation property)
        // - PropertyData (navigation property)

        return target;
    }

    /// <summary>
    /// Creates a new User entity with properties mapped from source
    /// </summary>
    /// <param name="source">Source User entity</param>
    /// <returns>New User entity with mapped properties</returns>
    public static User MapToNew(this User source)
    {
        var target = new User();
        return source.MapTo(target);
    }
}
