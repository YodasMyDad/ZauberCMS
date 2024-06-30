namespace ZauberCMS.Core.Extensions;

public static class TupleExtensions
{
    public static void SortList<T>((int oldIndex, int newIndex) indices, List<T> items)
    {
        var (oldIndex, newIndex) = indices;

        var itemToMove = items[oldIndex];
        items.RemoveAt(oldIndex);

        if (newIndex < items.Count)
        {
            items.Insert(newIndex, itemToMove);
        }
        else
        {
            items.Add(itemToMove);
        }
    }
}