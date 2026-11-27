using System.Reflection;
using Microsoft.AspNetCore.Components.Server.Circuits;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using ZauberCMS.Components.Editors;
using ZauberCMS.Core.Data;
using ZauberCMS.Core.Plugins;

namespace ZauberCMS.Core.Tests.Bootstrap;

/// <summary>
/// Roadmap Phase 11f, ZA-092 (D53): what the ZauberCMS packages make public is their public API, frozen by the
/// compatibility rule. The public types are pinned in <c>Snapshots/PublicTypes.txt</c>, so a type becomes public (or
/// stops being public) only on purpose; the plumbing kinds are internal; built-in extension types stay public
/// (discovery needs it); and EF still finds the internal mappings and migrations on every provider.
/// </summary>
public class PublicApiTests
{
    private const string SnapshotFile = "PublicTypes.txt";

    private static readonly Assembly Core = typeof(ZauberSetup).Assembly;
    private static readonly Assembly Components = typeof(TextProperty).Assembly;
    private static readonly Assembly Routing = Assembly.Load(new AssemblyName("ZauberCMS.Routing"));
    private static readonly Assembly[] Packages = [Core, Components, Routing];

    [Fact]
    public void ZA092_the_public_types_are_the_ones_in_the_snapshot()
    {
        var actual = Packages
            .SelectMany(a => a.GetExportedTypes().Select(t => $"{a.GetName().Name}: {t.FullName}"))
            .Order(StringComparer.Ordinal)
            .ToList();

        var snapshotPath = Path.Combine(AppContext.BaseDirectory, "Bootstrap", "Snapshots", SnapshotFile);
        var expected = File.ReadAllLines(snapshotPath)
            .Select(line => line.Trim())
            .Where(line => line.Length > 0 && !line.StartsWith('#'))
            .ToList();

        var removed = expected.Except(actual).ToList();
        var added = actual.Except(expected).ToList();

        Assert.True(removed.Count == 0,
            "Public types removed, renamed or made internal. That breaks sites built on them: give the old one an " +
            $"[Obsolete] path, or list it under Breaking: in the release notes, then remove the lines from " +
            $"Bootstrap/Snapshots/{SnapshotFile}: " + string.Join(", ", removed));
        Assert.True(added.Count == 0,
            "New public types are frozen API from their first release. Make each one internal unless a site needs " +
            $"it (Docs/Standards.md §1, \"What is public\"), else add it to Bootstrap/Snapshots/{SnapshotFile}: " +
            string.Join(", ", added));
    }

    // CultureMiddleware stays public: ZauberCMS.Components' EntryPage and SectionLayout call its static helpers, and
    // the packages use each other through public API only
    private static readonly HashSet<string> PublicMiddleware = ["ZauberCMS.Core.Middleware.CultureMiddleware"];

    [Fact]
    public void ZA092_ef_plumbing_middleware_hosted_services_and_circuit_handlers_are_internal()
    {
        Type[] plumbing =
        [
            typeof(IEntityTypeConfiguration<>), typeof(Migration), typeof(ModelSnapshot),
            typeof(IDesignTimeDbContextFactory<>), typeof(IHostedService), typeof(CircuitHandler),
        ];

        var publicPlumbing = Packages
            .SelectMany(a => a.GetExportedTypes())
            .Where(t => t.IsClass && plumbing.Any(p => Implements(t, p)))
            .Select(t => t.FullName)
            .ToList();
        Assert.Empty(publicPlumbing);

        // A conventional middleware: constructed with the next RequestDelegate
        var publicMiddleware = Packages
            .SelectMany(a => a.GetExportedTypes())
            .Where(t => t.IsClass && t.GetConstructors().Any(c => c.GetParameters().Any(p => p.ParameterType == typeof(RequestDelegate))))
            .Select(t => t.FullName!)
            .Where(name => !PublicMiddleware.Contains(name))
            .ToList();
        Assert.Empty(publicMiddleware);
    }

    [Fact]
    public void ZA092_every_built_in_extension_type_is_public()
    {
        // Discovery sees exported types only (AGENTS.md rule 3); the bootstrap-only kinds are not in the list
        var hidden = Packages
            .SelectMany(a => a.GetTypes())
            .Where(t => t is { IsClass: true, IsAbstract: false, IsVisible: false } &&
                        !t.IsDefined(typeof(System.Runtime.CompilerServices.CompilerGeneratedAttribute), false) &&
                        t.GetInterfaces().Select(i => i.IsGenericType ? i.GetGenericTypeDefinition() : i)
                            .Any(i => i.FullName != null && ExtensionManager.DiscoveryInterfaces.Contains(i.FullName)))
            .Select(t => t.FullName)
            .ToList();
        Assert.Empty(hidden);
    }

    public static TheoryData<string> Providers => [DatabaseProviders.SqlServer, DatabaseProviders.Sqlite, DatabaseProviders.PostgreSql];

    [Theory]
    [MemberData(nameof(Providers))]
    public void ZA092_ef_finds_the_internal_migrations_of_each_provider(string provider)
    {
        using var db = CreateContext(provider);

        var migrationTypes = Core.GetTypes()
            .Where(t => t.IsSubclassOf(typeof(Migration)) &&
                        t.GetCustomAttribute<DbContextAttribute>()?.ContextType == db.GetType())
            .ToList();
        Assert.NotEmpty(migrationTypes);
        Assert.All(migrationTypes, t => Assert.False(t.IsVisible, t.FullName));

        var expected = migrationTypes.Select(t => t.GetCustomAttribute<MigrationAttribute>()!.Id).Order(StringComparer.Ordinal);
        Assert.Equal(expected, db.Database.GetMigrations().Order(StringComparer.Ordinal));
        Assert.Contains(db.Database.GetMigrations(), id => id.EndsWith("_Initial", StringComparison.Ordinal));
    }

    [Theory]
    [MemberData(nameof(Providers))]
    public void ZA092_ef_applies_the_internal_entity_mappings_on_each_provider(string provider)
    {
        using var db = CreateContext(provider);

        var mapped = Core.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false })
            .SelectMany(t => t.GetInterfaces())
            .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEntityTypeConfiguration<>))
            .Select(i => i.GetGenericArguments()[0])
            .ToList();
        Assert.NotEmpty(mapped);

        // Every mapping names its table with the Zauber prefix; without it EF would use the DbSet name
        Assert.All(mapped, entity =>
        {
            var table = db.Model.FindEntityType(entity)?.GetTableName();
            Assert.True(table != null && table.StartsWith("Zauber", StringComparison.Ordinal),
                $"{entity.Name} is mapped to '{table}'");
        });
    }

    private static ZauberDbContextBase CreateContext(string provider)
    {
        // Nothing here connects: the migrations and the model come from the assembly
        var connectionString = provider switch
        {
            DatabaseProviders.SqlServer => "Server=127.0.0.1,1;Database=none",
            DatabaseProviders.PostgreSql => "Host=127.0.0.1;Port=1;Database=none",
            _ => "Data Source=:memory:",
        };
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Zauber:ConnectionString"] = connectionString })
            .Build();

        return provider switch
        {
            DatabaseProviders.SqlServer => new ZauberDbContext(new DbContextOptions<ZauberDbContext>(), configuration),
            DatabaseProviders.PostgreSql => new PostgreSqlZauberDbContext(new DbContextOptions<PostgreSqlZauberDbContext>(), configuration),
            _ => new SqliteZauberDbContext(new DbContextOptions<SqliteZauberDbContext>(), configuration),
        };
    }

    private static bool Implements(Type type, Type contract)
    {
        if (contract.IsInterface)
        {
            return type.GetInterfaces().Any(i => i == contract || (i.IsGenericType && i.GetGenericTypeDefinition() == contract));
        }

        for (var current = type.BaseType; current != null; current = current.BaseType)
        {
            if (current == contract)
            {
                return true;
            }
        }

        return false;
    }
}
