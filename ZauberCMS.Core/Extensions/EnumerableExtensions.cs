using System.Text;

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
    
    public static void RemoveWhere<T>(this List<T> list, Func<T, bool> predicate)
    {
        var itemsToRemove = list.Where(predicate).ToList();

        foreach(var item in itemsToRemove)
        {
            list.Remove(item);
        }
    }
    
    public static string ListToSingleString(this List<string> list, string lineBreak = "<br />")
    {
        var sb = new StringBuilder();
        for (var i = 0; i < list.Count; i++)
        {
            sb.Append(list[i]);
            if (i != list.Count - 1)
            {
                sb.Append(lineBreak);
            }
        }
        return sb.ToString();
    }
}