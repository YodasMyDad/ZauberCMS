namespace ZauberCMS.Core.Shared;

[Obsolete("Not used by ZauberCMS (ZA-092). Use Dictionary<(TKey1, TKey2), TValue>. Will be removed in a later major version.")]
public class Dictionary<TKey1,TKey2,TValue> :  Dictionary<Tuple<TKey1, TKey2>, TValue>
{

    public TValue this[TKey1 key1, TKey2 key2]
    {
        get => base[Tuple.Create(key1, key2)];
        set => base[Tuple.Create(key1, key2)] = value;
    }

    public void Add(TKey1 key1, TKey2 key2, TValue value)
    {
        base.Add(Tuple.Create(key1, key2), value);
    }

    public bool ContainsKey(TKey1 key1, TKey2 key2)
    {
        return base.ContainsKey(Tuple.Create(key1, key2));
    }
}