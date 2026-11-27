namespace ZauberCMS.Core.Shared.Services;

/// <summary>
/// Tells the other servers of a site that cache entries were cleared, so they drop theirs too. Each server keeps
/// its own cache (<see cref="ServerCache"/>); only invalidations travel. The default does nothing (one server);
/// with <c>Zauber:RedisConnectionString</c> set, Redis pub/sub is used. Register your own implementation as a
/// singleton to use another backplane.
/// </summary>
public interface ICacheInvalidationBus
{
    /// <summary>Called after this server removed <paramref name="key"/>.</summary>
    void PublishKeyCleared(string key);

    /// <summary>Called after this server removed every key starting with <paramref name="prefix"/>.</summary>
    void PublishPrefixCleared(string prefix);
}

/// <summary>One server: nothing to tell anyone.</summary>
public sealed class NoCacheInvalidationBus : ICacheInvalidationBus
{
    public void PublishKeyCleared(string key)
    {
    }

    public void PublishPrefixCleared(string prefix)
    {
    }
}
