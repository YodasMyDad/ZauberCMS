using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using ZauberCMS.Core.Shared.Services;

namespace ZauberCMS.Core.Tests.Shared;

/// <summary>ZA-115: requests that miss the same cache key at the same moment share one load.</summary>
public class CacheSingleLoadTests
{
    private static (DefaultCacheService Cache, ServerCache Server) Create()
    {
        var server = new ServerCache(new MemoryCache(new MemoryCacheOptions()));
        return (new DefaultCacheService(server, new NoCacheInvalidationBus()), server);
    }

    [Fact]
    public async Task Misses_at_the_same_moment_run_the_load_once()
    {
        var (cache, _) = Create();
        var loads = 0;
        var release = new TaskCompletionSource();

        async Task<string> Load()
        {
            Interlocked.Increment(ref loads);
            await release.Task;
            return "value";
        }

        var first = cache.GetSetCachedItemAsync("Thing-1", Load);
        var second = cache.GetSetCachedItemAsync("Thing-1", Load);
        release.SetResult();

        Assert.Equal(["value", "value"], await Task.WhenAll(first, second));
        Assert.Equal(1, loads);
    }

    [Fact]
    public async Task Requests_with_their_own_cache_service_share_one_load()
    {
        await using var site = await ZauberCMS.Core.Tests.Infrastructure.TestSite.CreateAsync(createUser: false);
        using var first = site.CreateScope();
        using var second = site.CreateScope();
        var loads = 0;
        var release = new TaskCompletionSource();

        async Task<string> Load()
        {
            Interlocked.Increment(ref loads);
            await release.Task;
            return "value";
        }

        var a = first.ServiceProvider.GetRequiredService<ICacheService>().GetSetCachedItemAsync("Thing-2", Load);
        var b = second.ServiceProvider.GetRequiredService<ICacheService>().GetSetCachedItemAsync("Thing-2", Load);
        release.SetResult();

        Assert.Equal(["value", "value"], await Task.WhenAll(a, b));
        Assert.Equal(1, loads);
    }

    [Fact]
    public async Task A_request_after_a_clear_does_not_get_the_load_that_started_before_it()
    {
        var (cache, _) = Create();
        var release = new TaskCompletionSource();
        var version = "before the save";

        async Task<string> Load()
        {
            var seen = version;
            await release.Task;
            return seen;
        }

        var early = cache.GetSetCachedItemAsync("Thing-1", Load);
        version = "after the save";
        cache.ClearCachedItemsWithPrefix("Thing");
        var late = cache.GetSetCachedItemAsync("Thing-1", Load);
        release.SetResult();

        Assert.Equal("before the save", await early);
        Assert.Equal("after the save", await late);
    }

    [Fact]
    public async Task A_failed_load_is_not_kept_and_the_next_request_loads_again()
    {
        var (cache, _) = Create();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            cache.GetSetCachedItemAsync<string>("Thing-1", () => throw new InvalidOperationException("down")));

        Assert.Equal("value", await cache.GetSetCachedItemAsync("Thing-1", () => Task.FromResult("value")));
    }
}
