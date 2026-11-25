using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ZauberCMS.Core.Extensions;
using ZauberCMS.Core.Content.Interfaces;
using ZauberCMS.Core.Content.Models;
using ZauberCMS.Core.Content.Parameters;
using ZauberCMS.Core.Tests.Infrastructure;
using ContentItem = ZauberCMS.Core.Content.Models.Content;

namespace ZauberCMS.Core.Tests.Shared;

/// <summary>Cache keys and invalidation on one server (Roadmap Phase 6, D13).</summary>
public class CacheTests : IAsyncLifetime
{
    private TestSite _site = null!;
    private ContentType _pageType = null!;

    public async ValueTask InitializeAsync()
    {
        _site = await TestSite.CreateAsync();
        _pageType = await _site.CreateContentTypeAsync("page");
    }

    public async ValueTask DisposeAsync() => await _site.DisposeAsync();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Cached_queries_with_different_filter_values_get_different_results()
    {
        var first = await _site.CreateContentAsync(_pageType, "First");
        var second = await _site.CreateContentAsync(_pageType, "Second");

        var a = await QueryAsync(new QueryContentParameters { Ids = [first.Id], Cached = true });
        var b = await QueryAsync(new QueryContentParameters { Ids = [second.Id], Cached = true });

        Assert.Equal(first.Id, Assert.Single(a).Id);
        Assert.Equal(second.Id, Assert.Single(b).Id);
    }

    [Fact]
    public async Task ZA072_A_cached_query_with_children_is_not_served_to_one_without_and_back()
    {
        var parent = await _site.CreateContentAsync(_pageType, "Parent");
        await _site.CreateContentAsync(_pageType, "Child", parent.Id);

        var without = await QueryAsync(new QueryContentParameters { Ids = [parent.Id], Cached = true });
        var with = await QueryAsync(new QueryContentParameters
            { Ids = [parent.Id], IncludeChildren = true, Cached = true });

        Assert.Empty(Assert.Single(without).Children);
        Assert.Single(Assert.Single(with).Children);
    }

    [Fact]
    public async Task ZA055_An_entry_cached_by_two_requests_at_once_is_still_cleared_by_its_prefix()
    {
        var cache = Cache();
        var key = $"Race-{Guid.NewGuid():N}";
        var gate = new TaskCompletionSource<int>();

        // Two misses at once: the slower one replaces the faster one's entry. The replaced entry's eviction
        // callback used to take the key out of the registry, so prefix clears no longer saw it.
        var slow = cache.GetSetCachedItemAsync(key, () => gate.Task);
        Assert.Equal(2, await cache.GetSetCachedItemAsync(key, () => Task.FromResult(2)));
        gate.SetResult(1);
        Assert.Equal(1, await slow);
        await Task.Delay(200, Ct); // eviction callbacks run on the thread pool

        cache.ClearCachedItemsWithPrefix("Race-");

        Assert.Equal(3, await cache.GetSetCachedItemAsync(key, () => Task.FromResult(3)));
    }

    [Fact]
    public async Task A_value_loaded_while_a_clear_ran_is_not_kept()
    {
        var cache = Cache();
        var key = $"Overlap-{Guid.NewGuid():N}";

        // The page was saved (and the prefix cleared) while the old value was being read
        Assert.Equal(1, await cache.GetSetCachedItemAsync(key, () =>
        {
            cache.ClearCachedItemsWithPrefix("Overlap-");
            return Task.FromResult(1);
        }));

        Assert.Equal(2, await cache.GetSetCachedItemAsync(key, () => Task.FromResult(2)));
    }

    [Fact]
    public async Task A_filtered_include_with_a_different_captured_value_gets_a_different_key()
    {
        var keys = await _site.QueryDatabaseAsync(db =>
        {
            string KeyFor(string name) => db.Contents
                .Include(c => c.Children.Where(child => child.Name == name))
                .AsSplitQuery()
                .GenerateCacheKey();
            return Task.FromResult((KeyFor("a"), KeyFor("b"), KeyFor("a")));
        });

        Assert.NotEqual(keys.Item1, keys.Item2);
        Assert.Equal(keys.Item1, keys.Item3);
    }

    private ZauberCMS.Core.Shared.Services.ICacheService Cache() =>
        _site.CreateScope().ServiceProvider.GetRequiredService<ZauberCMS.Core.Shared.Services.ICacheService>();

    private async Task<List<ContentItem>> QueryAsync(QueryContentParameters parameters)
    {
        using var scope = _site.CreateScope();
        var contentService = scope.ServiceProvider.GetRequiredService<IContentService>();
        return (await contentService.QueryContentAsync(parameters, Ct)).Items.ToList();
    }
}
