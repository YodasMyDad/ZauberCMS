using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using ZauberCMS.Core.Data;
using ZauberCMS.Core.Installer;
using ZauberCMS.Core.Installer.Interfaces;
using ZauberCMS.Core.Installer.Parameters;
using ZauberCMS.Core.Membership.Models;
using ZauberCMS.Core.Tests.Infrastructure;

namespace ZauberCMS.Core.Tests.Installer;

/// <summary>
/// A site that starts with no connection string runs no migrations at start-up: the installer's database step is
/// then the only thing that creates the schema. These tests run that step against real servers, on a database
/// that does not exist yet, and check that every migration is applied and the site works on it straight away.
/// (SQLite is covered by <see cref="DatabaseSetupTests"/>.)
/// </summary>
public class DatabaseSetupOnServerTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData(TestSite.PostgreSqlProvider)]
    [InlineData(TestSite.SqlServerProvider)]
    public async Task The_installer_creates_a_missing_database_with_every_table_and_the_site_installs_on_it(string provider)
    {
        // The helpers give us a server we may create databases on (or skip the test); the installer gets the
        // name of a database that is not there.
        string existing, target;
        if (provider == TestSite.PostgreSqlProvider)
        {
            existing = await TestContainers.CreatePostgreSqlDatabaseAsync();
            var builder = new NpgsqlConnectionStringBuilder(existing);
            builder.Database += "_installer";
            target = builder.ConnectionString;
        }
        else
        {
            existing = await TestContainers.CreateSqlServerDatabaseAsync();
            var builder = new SqlConnectionStringBuilder(existing);
            builder.InitialCatalog += "_installer";
            target = builder.ConnectionString;
        }

        var contentRoot = TestSite.CreateContentRoot();
        var app = TestSite.BuildApp("", "", contentRoot, validateContainer: false, readSettingsFile: true);
        try
        {
            var parameters = new DatabaseSetupParameters
            {
                Provider = provider,
                UseConnectionString = true,
                ConnectionString = target
            };

            using (var scope = app.Services.CreateScope())
            {
                var service = scope.ServiceProvider.GetRequiredService<IInstallService>();

                var test = await service.TestDatabaseAsync(parameters, Ct);
                Assert.True(test.Success, test.Message);
                Assert.False(test.DatabaseExists);

                var result = await service.SetUpDatabaseAsync(parameters, Ct);
                Assert.True(result.Success, string.Join("; ", result.Errors) + result.ManualConfigurationReason);
            }

            var state = app.Services.GetRequiredService<InstallState>();
            Assert.True(state.IsDatabaseConfigured);
            Assert.Equal(provider, state.DatabaseProvider);

            using (var scope = app.Services.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<IZauberDbContext>();
                Assert.Empty(await dbContext.Database.GetPendingMigrationsAsync(Ct));
                Assert.Equal(dbContext.Database.GetMigrations().Count(),
                    (await dbContext.Database.GetAppliedMigrationsAsync(Ct)).Count());
                // Every table of the model answers a query
                var model = ((DbContext)dbContext).Model;
                foreach (var table in model.GetEntityTypes().Select(t => t.GetTableName()).Where(t => t != null).Distinct())
                {
                    var quoted = provider == TestSite.PostgreSqlProvider ? $"\"{table}\"" : $"[{table}]";
#pragma warning disable EF1002 // Table names come from the model, not from input
                    await dbContext.Database.ExecuteSqlRawAsync($"SELECT COUNT(*) FROM {quoted}", Ct);
#pragma warning restore EF1002
                }

                var again = await scope.ServiceProvider.GetRequiredService<IInstallService>()
                    .TestDatabaseAsync(parameters, Ct);
                Assert.True(again.DatabaseExists);
            }

            using (var scope = app.Services.CreateScope())
            {
                var installed = await scope.ServiceProvider.GetRequiredService<IInstallService>().InstallAsync(
                    new InstallParameters
                    {
                        AdminName = "Ada Admin",
                        AdminEmail = "ada@example.test",
                        AdminPassword = "Passw0rd-For-Tests",
                        SiteName = "Installed on a server",
                        Language = "en-GB"
                    }, Ct);
                Assert.True(installed.Success, string.Join("; ", installed.Errors));
                Assert.Empty(installed.Warnings);
            }

            using (var scope = app.Services.CreateScope())
            {
                var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
                var admin = await userManager.FindByEmailAsync("ada@example.test");
                Assert.NotNull(admin);
                Assert.Equal(["Admin"], await userManager.GetRolesAsync(admin));
            }
        }
        finally
        {
            await app.DisposeAsync();
            if (provider == TestSite.PostgreSqlProvider)
            {
                NpgsqlConnection.ClearAllPools();
                await TestContainers.DropPostgreSqlDatabaseAsync(target);
                await TestContainers.DropPostgreSqlDatabaseAsync(existing);
            }
            else
            {
                SqlConnection.ClearAllPools();
                await TestContainers.DropSqlServerDatabaseAsync(target);
                await TestContainers.DropSqlServerDatabaseAsync(existing);
            }

            try
            {
                Directory.Delete(contentRoot, recursive: true);
            }
            catch (IOException)
            {
            }
        }
    }
}
