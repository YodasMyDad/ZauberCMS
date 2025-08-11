using ZauberCMS.Core.Data.Models;

namespace ZauberCMS.Core.Data.Mapping;

/// <summary>
/// Extension methods for mapping GlobalData entities
/// Replaces AutoMapper GlobalDataMapper profile
/// </summary>
public static class GlobalDataMappingExtensions
{
    /// <summary>
    /// Maps properties from source GlobalData to target GlobalData
    /// </summary>
    /// <param name="source">Source GlobalData entity</param>
    /// <param name="target">Target GlobalData entity to update</param>
    /// <returns>Updated target GlobalData entity</returns>
    public static GlobalData MapTo(this GlobalData source, GlobalData target)
    {
        // Map all properties (no exclusions in this case)
        target.Id = source.Id;
        target.Alias = source.Alias;
        target.Data = source.Data;
        target.DateCreated = source.DateCreated;
        target.DateUpdated = source.DateUpdated;

        return target;
    }

    /// <summary>
    /// Creates a new GlobalData entity with properties mapped from source
    /// </summary>
    /// <param name="source">Source GlobalData entity</param>
    /// <returns>New GlobalData entity with mapped properties</returns>
    public static GlobalData MapToNew(this GlobalData source)
    {
        var target = new GlobalData();
        return source.MapTo(target);
    }
}
