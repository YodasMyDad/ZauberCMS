namespace ZauberCMS.Core.Extensions;

public static class EnumerableExtensions
{
    /// <summary>
    ///     Filters a sequence of values to ignore those which are null.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="coll">The coll.</param>
    /// <returns></returns>
    /// <remarks></remarks>
    public static IEnumerable<T> WhereNotNull<T>(this IEnumerable<T?> coll)
        where T : class
        =>
            coll.Where(x => x != null)!;
}