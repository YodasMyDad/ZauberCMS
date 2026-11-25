using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;
using ZauberCMS.Core.Content.Interfaces;
using ZauberCMS.Core.Content.Models;
using ZauberCMS.Core.Content.Parameters;
using ZauberCMS.Core.Membership.Interfaces;
using ZauberCMS.Core.Shared.Services;
using ZauberCMS.Core.Tests.Infrastructure;

namespace ZauberCMS.Core.Tests.Shared;

/// <summary>
/// Sites with <c>Zauber:RedisConnectionString</c> set, against a real Redis (Docker; see <see cref="TestContainers"/>).
/// Two sites sharing one Redis stand in for two servers of one site (Roadmap D13).
/// </summary>
public class RedisCacheTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    // Waits until the server listens to Redis, as a started site does shortly after start
    private static async Task<TestSite> CreateServerAsync(string redis)
    {
        var site = await TestSite.CreateAsync(new Dictionary<string, string?> { ["Zauber:RedisConnectionString"] = redis });
        Assert.NotNull(await Bus(site).Ready);
        return site;
    }

    [Fact]
    public async Task ZA051_A_restricted_page_served_from_the_cache_keeps_its_roles()
    {
        var redis = await TestContainers.RedisConnectionStringAsync();
        await using var site = await CreateServerAsync(redis);
        var pageType = await site.CreateContentTypeAsync("page");
        var page = await site.CreateContentAsync(pageType, "Members only");
        var role = await site.QueryDatabaseAsync(async db =>
        {
            var membersRole = new ZauberCMS.Core.Membership.Models.Role { Name = "Members", NormalizedName = "MEMBERS" };
            db.Roles.Add(membersRole);
            db.ContentRoles.Add(new ZauberCMS.Core.Membership.Models.ContentRole { ContentId = page.Id, RoleId = membersRole.Id });
            await db.SaveChangesAsync(Ct);
            return membersRole;
        });

        // Twice: the second one comes from the cache. Through Redis the roles came back empty (they are
        // [JsonIgnore]), so EntryPage let anyone see the page.
        for (var i = 0; i < 2; i++)
        {
            using var scope = site.CreateScope();
            var contentService = scope.ServiceProvider.GetRequiredService<IContentService>();
            var result = await contentService.QueryContentAsync(new QueryContentParameters
                { Ids = [page.Id], IncludeContentRoles = true, Cached = true }, Ct);
            Assert.Equal(role.Id, Assert.Single(Assert.Single(result.Items).ContentRoles).RoleId);
        }
    }

    [Fact]
    public async Task ZA051_Users_and_their_password_hashes_are_not_written_to_Redis()
    {
        var redis = await TestContainers.RedisConnectionStringAsync();
        await using var site = await CreateServerAsync(redis);
        var user = await site.CreateUserAsync("cached-user");
        using (var scope = site.CreateScope())
        {
            var membershipService = scope.ServiceProvider.GetRequiredService<IMembershipService>();
            await membershipService.GetUserAsync(new ZauberCMS.Core.Membership.Parameters.GetUserParameters { Id = user.Id, Cached = true }, Ct);
        }

        Assert.True(site.Services.GetRequiredService<ServerCache>().Count > 0, "nothing was cached");

        await using var connection = await ConnectionMultiplexer.ConnectAsync(redis);
        var server = connection.GetServers().Single();
        await foreach (var key in server.KeysAsync(pattern: "*"))
        {
            var value = await connection.GetDatabase().KeyTypeAsync(key) == RedisType.String
                ? (string?)await connection.GetDatabase().StringGetAsync(key)
                : null;
            Assert.DoesNotContain("PasswordHash", value ?? string.Empty);
            Assert.DoesNotContain("cached-user", value ?? string.Empty);
        }
    }

    [Fact]
    public async Task A_clear_on_one_server_reaches_the_other()
    {
        var redis = await TestContainers.RedisConnectionStringAsync();
        await using var serverA = await CreateServerAsync(redis);
        await using var serverB = await CreateServerAsync(redis);
        var key = $"RedisTest-{Guid.NewGuid():N}";
        Assert.Equal(1, Cache(serverB).GetSetCachedItem(key, () => 1));

        Cache(serverA).ClearCachedItemsWithPrefix("RedisTest-");

        await EventuallyAsync(() => Task.FromResult(Cache(serverB).GetSetCachedItem(key, () => 2) == 2));
    }

    [Fact]
    public async Task A_server_that_missed_a_message_drops_everything()
    {
        var redis = await TestContainers.RedisConnectionStringAsync();
        await using var serverA = await CreateServerAsync(redis);
        await using var serverB = await CreateServerAsync(redis);
        var probe = $"RedisProbe-{Guid.NewGuid():N}";
        var unrelated = $"Unrelated-{Guid.NewGuid():N}";

        Assert.Equal(1, Cache(serverB).GetSetCachedItem(unrelated, () => 1));

        // A clear whose message never arrived: the counter moves on without a message
        await using (var connection = await ConnectionMultiplexer.ConnectAsync(redis))
        {
            var server = connection.GetServers().Single();
            await connection.GetDatabase().StringIncrementAsync(Bus(serverB).CounterKey);
        }

        // The next message shows B the gap, so it can't trust anything it holds, also the unrelated entry
        Cache(serverA).ClearCachedItem(probe);

        await EventuallyAsync(() => Task.FromResult(Cache(serverB).GetSetCachedItem(unrelated, () => 2) == 2));
    }

    [Fact]
    public async Task A_server_behind_the_counter_on_two_checks_drops_everything()
    {
        var redis = await TestContainers.RedisConnectionStringAsync();
        await using var server = await CreateServerAsync(redis);
        var bus = Bus(server);
        var key = $"Unrelated-{Guid.NewGuid():N}";
        Assert.Equal(1, Cache(server).GetSetCachedItem(key, () => 1));

        // A clear whose message was lost, and no later message to show the gap
        await using (var connection = await ConnectionMultiplexer.ConnectAsync(redis))
        {
            await connection.GetDatabase().StringIncrementAsync(bus.CounterKey);
        }

        // The first check may just be ahead of a message in flight; the second one is sure
        await bus.CheckAsync();
        await bus.CheckAsync();

        Assert.Equal(2, Cache(server).GetSetCachedItem(key, () => 2));
    }

    [Fact]
    public async Task A_server_drops_everything_when_Redis_lost_the_counter()
    {
        var redis = await TestContainers.RedisConnectionStringAsync();
        await using var serverA = await CreateServerAsync(redis);
        await using var serverB = await CreateServerAsync(redis);
        var probe = $"RedisProbe-{Guid.NewGuid():N}";
        var unrelated = $"Unrelated-{Guid.NewGuid():N}";
        Cache(serverA).ClearCachedItem(probe); // the counter is at least 1 and B has seen it
        await EventuallyAsync(async () =>
        {
            await using var c = await ConnectionMultiplexer.ConnectAsync(redis);
            return (long?)await c.GetDatabase().StringGetAsync(Bus(serverB).CounterKey) > 0;
        });
        await Task.Delay(200, Ct);
        Assert.Equal(1, Cache(serverB).GetSetCachedItem(unrelated, () => 1));

        // Redis restarted without persistence (or the key was flushed or evicted)
        await using (var connection = await ConnectionMultiplexer.ConnectAsync(redis))
        {
            await connection.GetDatabase().KeyDeleteAsync(Bus(serverB).CounterKey);
        }

        Cache(serverA).ClearCachedItem(probe);

        await EventuallyAsync(() => Task.FromResult(Cache(serverB).GetSetCachedItem(unrelated, () => 2) == 2));
    }

    private static RedisCacheInvalidationBus Bus(TestSite site) =>
        Assert.IsType<RedisCacheInvalidationBus>(site.Services.GetRequiredService<ICacheInvalidationBus>());

    // DefaultCacheService only holds singletons, so the scope can be left to the garbage collector
    private static ICacheService Cache(TestSite site) =>
        site.CreateScope().ServiceProvider.GetRequiredService<ICacheService>();

    private static async Task EventuallyAsync(Func<Task<bool>> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(15);
        while (!await condition())
        {
            Assert.True(DateTime.UtcNow < deadline, "Timed out waiting for the other server");
            await Task.Delay(100, Ct);
        }
    }
}
