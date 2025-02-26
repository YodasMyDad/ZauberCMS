using ZauberCMS.Core.Shared.Interfaces;

namespace ZauberCMS.Core.Shared.Comparers;

public class ITreeEqualityComparer<T> : IEqualityComparer<T> where T : class, ITreeItem
{
    public bool Equals(T? x, T? y)
    {
        if (x is null && y is null) return true;
        if (x is null || y is null) return false;
        return x.Id == y.Id; // Compare based on Id 
    }

    public int GetHashCode(T obj)
    {
        return obj.Id.GetHashCode(); // Use Id for hash code
    }
}
