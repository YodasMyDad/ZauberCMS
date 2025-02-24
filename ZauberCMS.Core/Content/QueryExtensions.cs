using Microsoft.EntityFrameworkCore;

namespace ZauberCMS.Core.Content;

public static class QueryExtensions
{
    /// <summary>
    /// Filters the Content entities based on whether their raw Path column contains the specified contentId.
    /// </summary>
    /// <param name="source">The source IQueryable of Content entities.</param>
    /// <param name="contentId">The content ID to look for in the Path column.</param>
    /// <returns>An IQueryable of Content entities matching the condition.</returns>
    public static IQueryable<Models.Content> WherePathLike(this DbSet<Models.Content> source, Guid contentId)
    {
        // Use EF.Functions.Like to avoid in-memory computation
        return source.FromSqlRaw($"""
                                   SELECT * 
                                   FROM ZauberContent
                                   WHERE Path LIKE '%"{contentId}"%'
                               """);
    }

}