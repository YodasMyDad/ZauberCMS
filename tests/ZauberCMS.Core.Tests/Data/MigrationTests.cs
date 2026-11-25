using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ZauberCMS.Core.Data;
using ZauberCMS.Core.Tests.Infrastructure;

namespace ZauberCMS.Core.Tests.Data;

/// <summary>
/// Schema rule from AGENTS.md: every schema change ships as three migrations (SQL Server, SQLite, PostgreSQL).
/// Pending-model-change warnings are suppressed in the DbContexts, so only tests can catch drift.
/// </summary>
public class MigrationTests
{
    private static readonly (string Provider, string ConnectionString, Type ContextType)[] Providers =
    [
        ("Sqlite", "DataSource=:memory:", typeof(SqliteZauberDbContext)),
        ("SqlServer", "Server=unused;Database=unused;User Id=unused;Password=unused;TrustServerCertificate=True",
            typeof(ZauberDbContext)),
        ("PostgreSql", "Host=unused;Database=unused;Username=unused;Password=unused",
            typeof(PostgreSqlZauberDbContext))
    ];

    [Fact]
    public async Task Empty_SQLite_database_migrates_and_has_nothing_pending()
    {
        await using var site = await TestSite.CreateAsync();

        var (applied, pending) = await site.QueryDatabaseAsync(async db =>
            ((await db.Database.GetAppliedMigrationsAsync()).ToList(),
                (await db.Database.GetPendingMigrationsAsync()).ToList()));

        Assert.NotEmpty(applied);
        Assert.Empty(pending);
    }

    // Compares the model built from the mappings with the provider's migration snapshot. No connection is opened.
    [Theory]
    [InlineData("Sqlite")]
    [InlineData("SqlServer")]
    [InlineData("PostgreSql")]
    public async Task Model_has_no_changes_missing_from_the_migrations(string provider)
    {
        await WithContextAsync(provider, db =>
            Assert.False(db.Database.HasPendingModelChanges(),
                $"{provider}: the entity mappings differ from the latest migration. " +
                "Run ./migrations.ps1 and commit all three providers."));
    }

    [Fact]
    public async Task All_three_providers_have_the_same_migrations_in_the_same_order()
    {
        var namesByProvider = new Dictionary<string, List<string>>();
        foreach (var (provider, _, _) in Providers)
        {
            // Migration ids are "<timestamp>_<Name>"; timestamps differ between providers, names must not.
            await WithContextAsync(provider, db => namesByProvider[provider] = db.Database.GetMigrations()
                .Select(id => id[(id.IndexOf('_') + 1)..])
                .ToList());
        }

        Assert.NotEmpty(namesByProvider["Sqlite"]);
        Assert.Equal(namesByProvider["Sqlite"], namesByProvider["SqlServer"]);
        Assert.Equal(namesByProvider["Sqlite"], namesByProvider["PostgreSql"]);
    }

    private static async Task WithContextAsync(string provider, Action<DbContext> action)
    {
        var (_, connectionString, contextType) = Providers.Single(p => p.Provider == provider);
        var contentRoot = TestSite.CreateContentRoot();
        try
        {
            await using var app = TestSite.BuildApp(provider, connectionString, contentRoot, validateContainer: false);
            using var scope = app.Services.CreateScope();
            action((DbContext)scope.ServiceProvider.GetRequiredService(contextType));
        }
        finally
        {
            Directory.Delete(contentRoot, recursive: true);
        }
    }
}
