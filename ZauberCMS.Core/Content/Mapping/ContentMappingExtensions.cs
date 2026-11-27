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
        target.Url = source.Url;
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
    /// Copies what an editor changes in a draft (name, URL, view, navigation settings) from a stored draft onto
    /// the live item, to show the draft in the editor. Everything else — where the item sits in the tree, its
    /// language, publish and recycle-bin state, dates — stays as the live item has it: those change through sort,
    /// move, culture and restore after the draft was saved, and the draft must not put them back (ZA-047).
    /// Property values are not copied; take them from the draft's <c>PropertyData</c>.
    /// </summary>
    /// <param name="draft">The stored draft (<c>UnpublishedContent.JsonContent</c>).</param>
    /// <param name="target">The live item, loaded from the database.</param>
    /// <returns>The updated <paramref name="target"/>.</returns>
    public static Models.Content MapDraftTo(this Models.Content draft, Models.Content target)
    {
        target.Name = draft.Name;
        target.Url = draft.Url;
        target.ViewComponent = draft.ViewComponent;
        target.HideFromNavigation = draft.HideFromNavigation;
        target.InternalRedirectId = draft.InternalRedirectId;
        return target;
    }

    /// <summary>
    /// <see cref="MapDraftTo"/> for a block (nested content), which also takes the draft's publish switch and
    /// property values: a block is published or hidden by the switch in its editor, as part of its page's draft.
    /// The property values are copies, so changing the target does not change the draft.
    /// </summary>
    /// <param name="draft">The block's stored draft (<c>UnpublishedContent.JsonContent</c>).</param>
    /// <param name="target">The block, loaded from the database.</param>
    /// <returns>The updated <paramref name="target"/>.</returns>
    public static Models.Content MapBlockDraftTo(this Models.Content draft, Models.Content target)
    {
        draft.MapDraftTo(target);
        target.Published = draft.Published;
        target.PropertyData = draft.PropertyData.Select(p => new Models.ContentPropertyValue
        {
            Id = p.Id,
            ContentId = target.Id,
            Alias = p.Alias,
            ContentTypePropertyId = p.ContentTypePropertyId,
            Value = p.Value,
            DateCreated = p.DateCreated,
            DateUpdated = p.DateUpdated
        }).ToList();
        target.InvalidateContentValuesCache();
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
