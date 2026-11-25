using Microsoft.EntityFrameworkCore;
using ZauberCMS.Core.Tests.Infrastructure;

namespace ZauberCMS.Core.Tests.Data;

/// <summary>
/// The site on a real PostgreSQL (Docker container, see <see cref="TestContainers"/>). Roadmap D1/D15: PostgreSQL
/// is a supported provider, so its migrations, raw SQL and queries are checked on the real thing.
/// </summary>
public class PostgreSqlTests
{
    [Fact]
    public async Task Empty_PostgreSQL_database_migrates_and_has_nothing_pending()
    {
        await using var site = await TestSite.CreateAsync(databaseProvider: TestSite.PostgreSqlProvider);

        var (applied, pending) = await site.QueryDatabaseAsync(async db =>
            ((await db.Database.GetAppliedMigrationsAsync()).ToList(),
                (await db.Database.GetPendingMigrationsAsync()).ToList()));

        Assert.NotEmpty(applied);
        Assert.Empty(pending);
    }
}
