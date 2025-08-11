using ZauberCMS.Core.Content.Models;

namespace ZauberCMS.Core.Content.Mapping;

/// <summary>
/// Extension methods for mapping ContentType entities
/// Replaces AutoMapper ContentTypeMapper profile
/// </summary>
public static class ContentTypeMappingExtensions
{
    /// <summary>
    /// Maps properties from source ContentType to target ContentType, excluding navigation properties
    /// </summary>
    /// <param name="source">Source ContentType entity</param>
    /// <param name="target">Target ContentType entity to update</param>
    /// <returns>Updated target ContentType entity</returns>
    public static ContentType MapTo(this ContentType source, ContentType target)
    {
        // Map all simple properties
        target.Id = source.Id;
        target.Name = source.Name;
        target.Description = source.Description;
        target.Alias = source.Alias;
        target.Icon = source.Icon;
        target.IsElementType = source.IsElementType;
        target.AllowAtRoot = source.AllowAtRoot;
        target.EnableListView = source.EnableListView;
        target.IncludeChildren = source.IncludeChildren;
        target.LastUpdatedById = source.LastUpdatedById;
        target.DateCreated = source.DateCreated;
        target.DateUpdated = source.DateUpdated;
        target.ContentProperties = source.ContentProperties;
        target.AvailableContentViews = source.AvailableContentViews;
        target.AllowedChildContentTypes = source.AllowedChildContentTypes;
        target.Tabs = source.Tabs;
        target.ParentId = source.ParentId;
        target.IsFolder = source.IsFolder;
        target.IsComposition = source.IsComposition;
        target.CompositionIds = source.CompositionIds;
        target.MediaId = source.MediaId;

        // Excluded properties (navigation properties):
        // - LinkedContent (navigation property)
        // - LastUpdatedBy (navigation property)

        return target;
    }

    /// <summary>
    /// Creates a new ContentType entity with properties mapped from source
    /// </summary>
    /// <param name="source">Source ContentType entity</param>
    /// <returns>New ContentType entity with mapped properties</returns>
    public static ContentType MapToNew(this ContentType source)
    {
        var target = new ContentType();
        return source.MapTo(target);
    }
}
