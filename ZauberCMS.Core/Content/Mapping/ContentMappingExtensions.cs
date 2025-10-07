namespace ZauberCMS.Core.Content.Mapping;

/// <summary>
/// Extension methods for mapping Content entities
/// Replaces AutoMapper ContentMapper profile
/// </summary>
public static class ContentMappingExtensions
{
    /// <summary>
    /// Maps properties from source Content to target Content, excluding navigation properties
    /// </summary>
    /// <param name="source">Source Content entity</param>
    /// <param name="target">Target Content entity to update</param>
    /// <returns>Updated target Content entity</returns>
    public static Models.Content MapTo(this Models.Content source, Models.Content target)
    {
        // Map all simple properties
        target.Id = source.Id;
        target.Name = source.Name;
#pragma warning disable CS0618 // Type or member is obsolete
        target.Url = source.Url;
#pragma warning restore CS0618 // Type or member is obsolete
        target.ContentTypeId = source.ContentTypeId;
        target.ContentTypeAlias = source.ContentTypeAlias;
        target.LastUpdatedById = source.LastUpdatedById;
        target.UnpublishedContentId = source.UnpublishedContentId;
        target.Path = source.Path;
        target.SortOrder = source.SortOrder;
        target.IsRootContent = source.IsRootContent;
        target.IsNestedContent = source.IsNestedContent;
        target.RelatedContentId = source.RelatedContentId;
        target.Published = source.Published;
        target.Deleted = source.Deleted;
        target.HideFromNavigation = source.HideFromNavigation;
        target.InternalRedirectId = source.InternalRedirectId;
        target.ParentId = source.ParentId;
        target.DateCreated = source.DateCreated;
        target.DateUpdated = source.DateUpdated;
        target.ViewComponent = source.ViewComponent;
        target.LanguageId = source.LanguageId;

        // Excluded properties (navigation properties and complex objects):
        // - ContentType (navigation property)
        // - Children (navigation property)
        // - Parent (navigation property)
        // - Audits (navigation property)
        // - PropertyData (navigation property)
        // - LastUpdatedBy (navigation property)
        // - Language (navigation property)
        // - UnpublishedContent (navigation property)
        // - InternalRedirectIdAsString (computed property)
        // - ContentRoles (navigation property)

        return target;
    }

    /// <summary>
    /// Creates a new Content entity with properties mapped from source
    /// </summary>
    /// <param name="source">Source Content entity</param>
    /// <returns>New Content entity with mapped properties</returns>
    public static Models.Content MapToNew(this Models.Content source)
    {
        var target = new Models.Content();
        return source.MapTo(target);
    }
}
