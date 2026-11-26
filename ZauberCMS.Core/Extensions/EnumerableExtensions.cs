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
    
    /// <summary>
    /// A dictionary of <paramref name="items"/> by alias where the first item with an alias wins, so stored values that
    /// repeat an alias (older data, or a clash through a composition) can still be read instead of throwing (ZA-028).
    /// Aliases compare exactly, as before.
    /// </summary>
    public static Dictionary<string, TValue> ToValuesByAlias<T, TValue>(this IEnumerable<T> items,
        Func<T, string?> aliasOf, Func<T, TValue> valueOf)
    {
        var result = new Dictionary<string, TValue>();
        foreach (var item in items)
        {
            if (aliasOf(item) is { } alias)
            {
                result.TryAdd(alias, valueOf(item));
            }
        }

        return result;
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