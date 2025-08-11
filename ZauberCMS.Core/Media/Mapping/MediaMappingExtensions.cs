using ZauberCMS.Core.Media.Models;

namespace ZauberCMS.Core.Media.Mapping;

/// <summary>
/// Extension methods for mapping Media entities
/// Replaces AutoMapper MediaMapper profile
/// </summary>
public static class MediaMappingExtensions
{
    /// <summary>
    /// Maps properties from source Media to target Media, excluding navigation properties
    /// </summary>
    /// <param name="source">Source Media entity</param>
    /// <param name="target">Target Media entity to update</param>
    /// <returns>Updated target Media entity</returns>
    public static Models.Media MapTo(this Models.Media source, Models.Media target)
    {
        // Map all simple properties
        target.Id = source.Id;
        target.Url = source.Url;
        target.SortOrder = source.SortOrder;
        target.Name = source.Name;
        target.AltTag = source.AltTag;
        target.MediaType = source.MediaType;
        target.FileSize = source.FileSize;
        target.Width = source.Width;
        target.Height = source.Height;
        target.ParentId = source.ParentId;
        target.Path = source.Path;
        target.LastUpdatedById = source.LastUpdatedById;
        target.DateCreated = source.DateCreated;
        target.DateUpdated = source.DateUpdated;
        target.ExtendedData = source.ExtendedData;
        target.Deleted = source.Deleted;
        target.RequiresAuthentication = source.RequiresAuthentication;

        // Excluded properties (navigation properties):
        // - Children (navigation property)
        // - Audits (navigation property)
        // - Parent (navigation property)
        // - LastUpdatedBy (navigation property)
        // - MediaRoles (navigation property)

        return target;
    }

    /// <summary>
    /// Creates a new Media entity with properties mapped from source
    /// </summary>
    /// <param name="source">Source Media entity</param>
    /// <returns>New Media entity with mapped properties</returns>
    public static Models.Media MapToNew(this Models.Media source)
    {
        var target = new Models.Media();
        return source.MapTo(target);
    }
}
