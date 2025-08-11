using ZauberCMS.Core.Seo.Models;

namespace ZauberCMS.Core.Seo.Mapping;

/// <summary>
/// Extension methods for mapping SeoRedirect entities
/// Replaces AutoMapper SeoRedirectMapper profile
/// </summary>
public static class SeoRedirectMappingExtensions
{
    /// <summary>
    /// Maps properties from source SeoRedirect to target SeoRedirect, excluding navigation properties
    /// </summary>
    /// <param name="source">Source SeoRedirect entity</param>
    /// <param name="target">Target SeoRedirect entity to update</param>
    /// <returns>Updated target SeoRedirect entity</returns>
    public static SeoRedirect MapTo(this SeoRedirect source, SeoRedirect target)
    {
        // Map all simple properties
        target.Id = source.Id;
        target.Name = source.Name;
        target.DomainId = source.DomainId;
        target.FromUrl = source.FromUrl;
        target.ToUrl = source.ToUrl;
        target.IsPermanent = source.IsPermanent;
        target.DateCreated = source.DateCreated;
        target.DateUpdated = source.DateUpdated;

        // Excluded properties (navigation properties):
        // - Domain (navigation property)

        return target;
    }

    /// <summary>
    /// Creates a new SeoRedirect entity with properties mapped from source
    /// </summary>
    /// <param name="source">Source SeoRedirect entity</param>
    /// <returns>New SeoRedirect entity with mapped properties</returns>
    public static SeoRedirect MapToNew(this SeoRedirect source)
    {
        var target = new SeoRedirect();
        return source.MapTo(target);
    }
}
