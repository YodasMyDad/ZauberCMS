using ZauberCMS.Core.Audit.Models;

namespace ZauberCMS.Core.Audit.Mapping;

/// <summary>
/// Extension methods for mapping Audit entities
/// Replaces AutoMapper AuditMapper profile
/// </summary>
public static class AuditMappingExtensions
{
    /// <summary>
    /// Maps properties from source Audit to target Audit
    /// </summary>
    /// <param name="source">Source Audit entity</param>
    /// <param name="target">Target Audit entity to update</param>
    /// <returns>Updated target Audit entity</returns>
    public static Models.Audit MapTo(this Models.Audit source, Models.Audit target)
    {
        // Map all properties (no exclusions in this case)
        target.Id = source.Id;
        target.DateCreated = source.DateCreated;
        target.DateUpdated = source.DateUpdated;
        target.Description = source.Description;

        return target;
    }

    /// <summary>
    /// Creates a new Audit entity with properties mapped from source
    /// </summary>
    /// <param name="source">Source Audit entity</param>
    /// <returns>New Audit entity with mapped properties</returns>
    public static Models.Audit MapToNew(this Models.Audit source)
    {
        var target = new Models.Audit();
        return source.MapTo(target);
    }
}
