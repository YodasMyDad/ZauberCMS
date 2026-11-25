using System.Globalization;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using ZauberCMS.Core.Settings;

namespace ZauberCMS.Core.Shared.Services;

/// <summary>
/// Passes cache clears between the servers of a site through Redis (Roadmap D13). Each clear increments a counter
/// and publishes "counter|server|kind|key" in one atomic script. This server drops everything it cached when it
/// can't be sure it heard every clear: the counter jumps (a message was missed), the counter is ahead of what
/// arrived on two checks in a row, the counter went backwards (Redis was reset), or it (re)connects to Redis.
/// Clears that could not be sent are followed by one "clear everything" once Redis is reachable again.
/// </summary>
internal sealed class RedisCacheInvalidationBus : ICacheInvalidationBus, IDisposable
{
    private const string PublishScript = """
                                         local v = redis.call('INCR', KEYS[1])
                                         redis.call('PUBLISH', ARGV[1], v .. '|' .. ARGV[2])
                                         return v
                                         """;

    internal static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(30);

    private readonly ServerCache _serverCache;
    private readonly ILogger<RedisCacheInvalidationBus> _logger;
    private readonly string _serverId = Guid.NewGuid().ToString("N");
    private readonly RedisChannel _channel;
    private readonly RedisKey _counterKey;
    private readonly ConfigurationOptions _options;
    private readonly Lock _sync = new();
    private readonly Timer _timer;

    private Task<ConnectionMultiplexer?> _connection;
    private volatile ConnectionMultiplexer? _redis; // set as soon as connected, before the subscription is ready
    private volatile bool _disposed;

    // Guarded by _sync
    private long? _lastSeen;      // highest counter this server has applied; null = not in step (resync needed)
    private long? _behindAt;      // counter seen ahead of _lastSeen on the last check
    private bool _missedPublish;  // a clear could not be sent

    public RedisCacheInvalidationBus(ServerCache serverCache, IOptions<ZauberSettings> settings,
        IHostEnvironment environment, ILogger<RedisCacheInvalidationBus> logger)
    {
        _serverCache = serverCache;
        _logger = logger;
        var prefix = $"zauber:{environment.ApplicationName}:cache";
        _channel = RedisChannel.Literal(prefix);
        _counterKey = prefix + ":counter";

        _options = ConfigurationOptions.Parse(settings.Value.RedisConnectionString!);
        _options.AbortOnConnectFail = false; // keep retrying; the site runs on its own cache meanwhile
        _connection = ConnectAsync();
        _timer = new Timer(_ => _ = CheckAsync(), null, CheckInterval, CheckInterval);
    }

    /// <summary>The first connection attempt: null if it failed (tests wait for it).</summary>
    internal Task<ConnectionMultiplexer?> Ready => _connection;

    /// <summary>The Redis key holding the counter.</summary>
    internal string CounterKey => _counterKey.ToString();

    public void PublishKeyCleared(string key) => _ = PublishAsync('k', key);

    public void PublishPrefixCleared(string prefix) => _ = PublishAsync('p', prefix);

    private async Task PublishAsync(char kind, string value)
    {
        var redis = _redis;
        if (redis is { IsConnected: true })
        {
            try
            {
                await redis.GetDatabase().ScriptEvaluateAsync(PublishScript, [_counterKey],
                    [_channel.ToString(), $"{_serverId}|{kind}|{value}"]);
                return;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "A cache clear could not be sent to the other servers; sending a full clear later");
            }
        }

        lock (_sync)
        {
            _missedPublish = true;
        }
    }

    private async Task<ConnectionMultiplexer?> ConnectAsync()
    {
        ConnectionMultiplexer? redis = null;
        try
        {
            redis = await ConnectionMultiplexer.ConnectAsync(_options);
            if (_disposed)
            {
                redis.Dispose();
                return null;
            }

            _redis = redis;
            redis.ConnectionRestored += (_, _) => _ = ResyncAsync(redis, "the connection to Redis was restored");
            var queue = await redis.GetSubscriber().SubscribeAsync(_channel);
            queue.OnMessage(message => Apply(message.Message.ToString()));
            if (redis.IsConnected)
            {
                await ResyncAsync(redis, null);
            }

            return redis;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not set up Redis cache invalidation; trying again in {Interval}", CheckInterval);
            _redis = null;
            redis?.Dispose();
            lock (_sync)
            {
                _missedPublish = true; // clears made until then were not sent
                _lastSeen = null;
            }

            return null;
        }
    }

    // Start (again) from the current counter with an empty cache: anything cached meanwhile may be stale.
    private async Task ResyncAsync(ConnectionMultiplexer redis, string? reason)
    {
        try
        {
            var counter = (long?)await redis.GetDatabase().StringGetAsync(_counterKey) ?? 0;
            bool sendClearAll;
            lock (_sync)
            {
                // A counter below what was seen means Redis lost it (restart, flush, eviction): start from it
                _lastSeen = _lastSeen is { } seen && seen > counter ? counter : Math.Max(_lastSeen ?? 0, counter);
                _behindAt = null;
                sendClearAll = _missedPublish;
                _missedPublish = false;
            }

            if (reason != null)
            {
                _logger.LogInformation("Cache cleared because {Reason}", reason);
            }

            _serverCache.RemoveAll();
            if (sendClearAll)
            {
                // Others never heard about the clears that could not be sent
                PublishPrefixCleared(string.Empty);
            }
        }
        catch (Exception ex)
        {
            lock (_sync)
            {
                _lastSeen = null; // the next check tries again
            }

            _logger.LogWarning(ex, "Redis cache invalidation: resync failed; trying again in {Interval}", CheckInterval);
        }
    }

    internal void Apply(string message)
    {
        var parts = message.Split('|', 4);
        if (parts.Length != 4 ||
            !long.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var counter))
        {
            return;
        }

        string? clearAllReason = null;
        lock (_sync)
        {
            if (_lastSeen is { } seen)
            {
                if (counter > seen + 1)
                {
                    clearAllReason = "a cache invalidation message from another server was missed";
                }
                else if (counter <= seen && parts[1] != _serverId)
                {
                    // Pub/sub keeps the order, so a lower counter means Redis lost it (restart, flush, eviction)
                    clearAllReason = "the Redis cache invalidation counter was reset";
                }

                _lastSeen = clearAllReason != null ? counter : Math.Max(seen, counter);
            }
        }

        if (clearAllReason != null)
        {
            _logger.LogInformation("Cache cleared because {Reason}", clearAllReason);
            _serverCache.RemoveAll();
            return;
        }

        if (parts[1] == _serverId)
        {
            return; // cleared here already
        }

        switch (parts[2])
        {
            case "k":
                _serverCache.Remove(parts[3]);
                break;
            case "p":
                _serverCache.RemoveWithPrefix(parts[3]);
                break;
        }
    }

    // Pub/sub delivers at most once. If the counter is ahead of what arrived, and still is one check later (so the
    // message was not just in flight), this server missed something. Also retries a failed connection or resync.
    internal async Task CheckAsync()
    {
        try
        {
            if (_disposed)
            {
                return;
            }

            if (_connection is { IsCompletedSuccessfully: true, Result: null })
            {
                _connection = ConnectAsync();
                return;
            }

            var redis = _redis;
            if (redis is not { IsConnected: true })
            {
                return;
            }

            bool resync;
            lock (_sync)
            {
                resync = _lastSeen == null || _missedPublish;
            }

            if (resync)
            {
                await ResyncAsync(redis, "cache invalidation was out of step with Redis");
                return;
            }

            var counter = (long?)await redis.GetDatabase().StringGetAsync(_counterKey) ?? 0;
            string? clearAllReason = null;
            lock (_sync)
            {
                var seen = _lastSeen ?? 0;
                if (counter < seen)
                {
                    clearAllReason = "the Redis cache invalidation counter was reset";
                    _lastSeen = counter;
                    _behindAt = null;
                }
                else
                {
                    if (_behindAt is { } behindAt && seen < behindAt)
                    {
                        clearAllReason = "this server is behind the Redis cache invalidation counter";
                        _lastSeen = seen = behindAt;
                    }

                    _behindAt = counter > seen ? counter : null;
                }
            }

            if (clearAllReason != null)
            {
                _logger.LogInformation("Cache cleared because {Reason}", clearAllReason);
                _serverCache.RemoveAll();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis cache invalidation: check failed");
        }
    }

    public void Dispose()
    {
        _disposed = true;
        _timer.Dispose();
        _redis?.Dispose();
        _redis = null;
    }
}

/// <summary>
/// Creates the invalidation bus when the site starts, so it connects and listens before the first request.
/// Startup does not wait for Redis: an unreachable Redis must not keep the site down.
/// </summary>
internal sealed class CacheInvalidationStartup : IHostedService
{
    // Being given the bus is what creates it (it is a singleton)
    public CacheInvalidationStartup(ICacheInvalidationBus invalidationBus) => GC.KeepAlive(invalidationBus);

    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
