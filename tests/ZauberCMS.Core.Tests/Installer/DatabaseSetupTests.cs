using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ZauberCMS.Core.Data;
using ZauberCMS.Core.Installer;
using ZauberCMS.Core.Installer.Interfaces;
using ZauberCMS.Core.Installer.Parameters;
using ZauberCMS.Core.Tests.Infrastructure;

namespace ZauberCMS.Core.Tests.Installer;

/// <summary>
/// Roadmap D19: a site without a database starts, the installer creates the database, writes the choice into
/// appsettings.json and the running site uses it.
/// </summary>
public class DatabaseSetupTests : IAsyncLifetime
{
    private readonly string _contentRoot = TestSite.CreateContentRoot();
    private WebApplication? _app;

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public async ValueTask DisposeAsync()
    {
        if (_app != null)
        {
            await _app.DisposeAsync();
        }

        // The SQLite file is in the content root: let go of it before the folder is removed
        SqliteConnection.ClearAllPools();
        try
        {
            Directory.Delete(_contentRoot, recursive: true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private string SettingsFile => Path.Combine(_contentRoot, "appsettings.json");

    private string DatabaseFile => Path.Combine(_contentRoot, "installed.db");

    // A site as the template makes it: both keys present and empty
    private WebApplication SiteWithoutDatabase(bool readSettingsFile = true, string provider = "", string connectionString = "") =>
        _app = TestSite.BuildApp(provider, connectionString, _contentRoot, validateContainer: false,
            readSettingsFile: readSettingsFile);

    private DatabaseSetupParameters Sqlite() => new()
    {
        Provider = DatabaseProviders.Sqlite,
        SqliteFile = DatabaseFile
    };

    [Theory]
    [InlineData("", "")]
    [InlineData("Sqlite", "")]
    [InlineData("", "DataSource=app.db")]
    [InlineData("  ", "DataSource=app.db")]
    public void A_site_with_an_empty_provider_or_connection_string_starts_without_a_database(string provider,
        string connectionString)
    {
        var app = SiteWithoutDatabase(readSettingsFile: false, provider, connectionString);

        var state = app.Services.GetRequiredService<InstallState>();
        Assert.False(state.IsDatabaseConfigured);
        Assert.False(state.IsInstalled);
    }

    [Fact]
    public void An_unknown_provider_is_still_a_configuration_error()
    {
        var exception = Assert.ThrowsAny<Exception>(() =>
            SiteWithoutDatabase(readSettingsFile: false, "MySql", "Server=x"));

        Assert.Contains("database provider", exception.Message);
    }

    [Fact]
    public void Without_a_database_asking_for_the_context_says_what_to_do()
    {
        var app = SiteWithoutDatabase();
        using var scope = app.Services.CreateScope();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            scope.ServiceProvider.GetRequiredService<IZauberDbContext>());

        Assert.Contains("/install", exception.Message);
    }

    [Fact]
    public async Task Setting_up_the_database_creates_it_writes_the_settings_file_and_switches_the_site_to_it()
    {
        // The file a new site has: other keys must survive
        await File.WriteAllTextAsync(SettingsFile,
            """{ "Zauber": { "DatabaseProvider": "", "ConnectionString": "", "UploadFolderName": "media" }, "AllowedHosts": "*" }""",
            Ct);
        var app = SiteWithoutDatabase();
        var state = app.Services.GetRequiredService<InstallState>();
        Assert.False(state.IsDatabaseConfigured);

        using var scope = app.Services.CreateScope();
        var result = await scope.ServiceProvider.GetRequiredService<IInstallService>().SetUpDatabaseAsync(Sqlite(), Ct);

        Assert.True(result.Success, string.Join("; ", result.Errors) + result.ManualConfigurationReason);
        Assert.True(state.IsDatabaseConfigured);
        Assert.True(state.IsDatabaseInitialised);
        Assert.Equal(DatabaseProviders.Sqlite, state.DatabaseProvider);
        Assert.False(state.IsInstalled);

        using var written = JsonDocument.Parse(await File.ReadAllTextAsync(SettingsFile, Ct));
        var zauber = written.RootElement.GetProperty("Zauber");
        Assert.Equal("Sqlite", zauber.GetProperty("DatabaseProvider").GetString());
        Assert.Contains("installed.db", zauber.GetProperty("ConnectionString").GetString());
        Assert.Equal("media", zauber.GetProperty("UploadFolderName").GetString());
        Assert.Equal("*", written.RootElement.GetProperty("AllowedHosts").GetString());

        // The running site now works on the new database, with no restart
        using var afterwards = app.Services.CreateScope();
        var dbContext = afterwards.ServiceProvider.GetRequiredService<IZauberDbContext>();
        Assert.IsType<SqliteZauberDbContext>(dbContext);
        Assert.Empty(await dbContext.Database.GetPendingMigrationsAsync(Ct));
        Assert.False(await dbContext.Users.AnyAsync(Ct));
    }

    [Fact]
    public async Task A_settings_file_that_other_configuration_overrides_asks_for_the_change_by_hand()
    {
        // No file source in this site: writing appsettings.json cannot change what the site reads
        var app = SiteWithoutDatabase(readSettingsFile: false);

        using var scope = app.Services.CreateScope();
        var result = await scope.ServiceProvider.GetRequiredService<IInstallService>().SetUpDatabaseAsync(Sqlite(), Ct);

        Assert.False(result.Success);
        Assert.True(result.ManualConfigurationRequired);
        // The file was written; it is the override the owner has to deal with
        Assert.True(result.SettingsFileWritten);
        Assert.Equal(DatabaseProviders.Sqlite, result.DatabaseProvider);
        Assert.Contains("installed.db", result.ConnectionString);
        Assert.False(app.Services.GetRequiredService<InstallState>().IsDatabaseConfigured);
        // The database itself is ready for when the owner has set the values
        Assert.True(File.Exists(DatabaseFile));
    }

    // A wrong provider typed into the file by hand can be corrected without restarting: nothing is fixed
    // until a database has really been prepared
    [Fact]
    public async Task Until_a_database_works_the_provider_follows_the_settings_file()
    {
        // The site starts with no database; the owner then types the settings into the file, wrongly at first
        var app = SiteWithoutDatabase();
        var state = app.Services.GetRequiredService<InstallState>();
        var configuration = (IConfigurationRoot)app.Services.GetRequiredService<IConfiguration>();
        await File.WriteAllTextAsync(SettingsFile,
            """{ "Zauber": { "DatabaseProvider": "SqlServer", "ConnectionString": "DataSource=x.db" } }""", Ct);
        configuration.Reload();
        Assert.True(state.IsDatabaseConfigured);
        Assert.Equal(DatabaseProviders.SqlServer, state.DatabaseProvider);

        await File.WriteAllTextAsync(SettingsFile,
            $$"""{ "Zauber": { "DatabaseProvider": "Sqlite", "ConnectionString": "DataSource={{DatabaseFile.Replace("\\", "/")}}" } }""", Ct);
        configuration.Reload();

        Assert.Equal(DatabaseProviders.Sqlite, state.DatabaseProvider);
        using var scope = app.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IInstallService>().InitialiseDatabaseAsync(Ct);
        Assert.IsType<SqliteZauberDbContext>(scope.ServiceProvider.GetRequiredService<IZauberDbContext>());

        // Now it is in use: a later edit waits for the next start
        await File.WriteAllTextAsync(SettingsFile,
            """{ "Zauber": { "DatabaseProvider": "PostgreSql", "ConnectionString": "Host=elsewhere" } }""", Ct);
        configuration.Reload();
        Assert.Equal(DatabaseProviders.Sqlite, state.DatabaseProvider);
    }

    [Fact]
    public async Task A_database_that_cannot_be_reached_changes_nothing()
    {
        var app = SiteWithoutDatabase();
        using var scope = app.Services.CreateScope();

        var result = await scope.ServiceProvider.GetRequiredService<IInstallService>().SetUpDatabaseAsync(
            new DatabaseSetupParameters
            {
                Provider = DatabaseProviders.Sqlite,
                SqliteFile = Path.Combine(_contentRoot, "no-such-folder", "site.db")
            }, Ct);

        Assert.False(result.Success);
        Assert.False(result.ManualConfigurationRequired);
        Assert.NotEmpty(result.Errors);
        Assert.False(File.Exists(SettingsFile));
        Assert.False(app.Services.GetRequiredService<InstallState>().IsDatabaseConfigured);
    }

    [Fact]
    public async Task A_site_that_has_a_database_refuses_the_database_step()
    {
        await using var site = await TestSite.CreateAsync(createUser: false);
        using var scope = site.CreateScope();

        var result = await scope.ServiceProvider.GetRequiredService<IInstallService>().SetUpDatabaseAsync(Sqlite(), Ct);

        Assert.False(result.Success);
        Assert.False(File.Exists(DatabaseFile));
    }

    [Fact]
    public async Task Testing_a_connection_changes_nothing()
    {
        var app = SiteWithoutDatabase();
        using var scope = app.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IInstallService>();

        var fine = await service.TestDatabaseAsync(Sqlite(), Ct);
        var noFolder = await service.TestDatabaseAsync(new DatabaseSetupParameters
        {
            Provider = DatabaseProviders.Sqlite,
            SqliteFile = Path.Combine(_contentRoot, "no-such-folder", "site.db")
        }, Ct);
        var notADatabase = Path.Combine(_contentRoot, "notes.txt");
        await File.WriteAllTextAsync(notADatabase, new string('x', 4096), Ct);
        var wrongFile = await service.TestDatabaseAsync(
            new DatabaseSetupParameters { Provider = DatabaseProviders.Sqlite, SqliteFile = notADatabase }, Ct);

        var share = await service.TestDatabaseAsync(
            new DatabaseSetupParameters { Provider = DatabaseProviders.Sqlite, SqliteFile = @"\\files.example\share\site.db" }, Ct);
        Assert.False(share.Success);
        Assert.Contains("network share", share.Message);

        Assert.True(fine.Success);
        Assert.False(fine.DatabaseExists);
        Assert.False(File.Exists(DatabaseFile));
        Assert.False(noFolder.Success);
        Assert.False(wrongFile.Success);
        Assert.Equal(new string('x', 4096), await File.ReadAllTextAsync(notADatabase, Ct));
    }

    [Fact]
    public void Connection_strings_are_built_by_the_provider_so_a_value_cannot_add_keywords()
    {
        var sqlServer = new DatabaseSetupParameters
        {
            Provider = "sqlserver",
            Server = @".\SQLEXPRESS",
            Database = "Zauber;Integrated Security=true",
            Username = "site",
            Password = "p;a=ss'word",
            TrustServerCertificate = true
        }.BuildConnectionString();
        var parsedSqlServer = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(sqlServer);
        Assert.Equal("Zauber;Integrated Security=true", parsedSqlServer.InitialCatalog);
        Assert.False(parsedSqlServer.IntegratedSecurity);
        Assert.Equal("p;a=ss'word", parsedSqlServer.Password);
        Assert.True(parsedSqlServer.TrustServerCertificate);

        var postgres = new DatabaseSetupParameters
        {
            Provider = DatabaseProviders.PostgreSql,
            Server = "db.internal",
            Port = 6543,
            Database = "zauber",
            Username = "site",
            Password = "se;cret"
        }.BuildConnectionString();
        var parsedPostgres = new Npgsql.NpgsqlConnectionStringBuilder(postgres);
        Assert.Equal("db.internal", parsedPostgres.Host);
        Assert.Equal(6543, parsedPostgres.Port);
        Assert.Equal("se;cret", parsedPostgres.Password);

        var windows = new DatabaseSetupParameters
        {
            Provider = DatabaseProviders.SqlServer,
            Server = "localhost",
            Database = "Zauber",
            IntegratedSecurity = true
        }.BuildConnectionString();
        Assert.True(new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(windows).IntegratedSecurity);
    }

    [Theory]
    [InlineData("SqlServer", "", "db", "user")]
    [InlineData("SqlServer", "server", "", "user")]
    [InlineData("SqlServer", "server", "db", "")]
    [InlineData("PostgreSql", "server", "db", "")]
    [InlineData("Oracle", "server", "db", "user")]
    public void Missing_database_details_are_named(string provider, string server, string database, string username)
    {
        var parameters = new DatabaseSetupParameters
        {
            Provider = provider, Server = server, Database = database, Username = username
        };

        Assert.Throws<ArgumentException>(() => parameters.BuildConnectionString());
    }

    [Fact]
    public void A_typed_connection_string_must_make_sense_for_the_provider()
    {
        var parameters = new DatabaseSetupParameters
        {
            Provider = DatabaseProviders.SqlServer,
            UseConnectionString = true,
            ConnectionString = "Server=.;Database=Zauber;Nonsense Keyword=1"
        };

        Assert.Throws<ArgumentException>(() => parameters.BuildConnectionString());

        parameters.ConnectionString = " Server=.;Database=Zauber;Integrated Security=true ";
        Assert.Equal("Server=.;Database=Zauber;Integrated Security=true", parameters.BuildConnectionString());
    }

    [Fact]
    public void The_settings_writer_keeps_the_rest_of_the_file_and_its_byte_order_mark()
    {
        var bom = Encoding.UTF8.GetPreamble();
        var original = """
                       {
                         // comments are tolerated when reading
                         "Serilog": { "MinimumLevel": { "Default": "Warning" } },
                         "zauber": { "databaseProvider": "", "ConnectionString": "", "SiteUrl": "https://example.test" },
                       }
                       """;
        File.WriteAllBytes(SettingsFile, [.. bom, .. Encoding.UTF8.GetBytes(original)]);

        var result = AppSettingsWriter.Write(SettingsFile, "PostgreSql", "Host=db;Password=it's+a=secret");

        Assert.True(result.Success, result.Error);
        var bytes = File.ReadAllBytes(SettingsFile);
        Assert.True(bytes.AsSpan().StartsWith(bom));
        using var written = JsonDocument.Parse(bytes.AsMemory(bom.Length));
        // The existing spelling of the keys is reused: configuration keys are not case-sensitive
        var zauber = written.RootElement.GetProperty("zauber");
        Assert.Equal("PostgreSql", zauber.GetProperty("databaseProvider").GetString());
        Assert.Equal("Host=db;Password=it's+a=secret", zauber.GetProperty("ConnectionString").GetString());
        Assert.Equal("https://example.test", zauber.GetProperty("SiteUrl").GetString());
        Assert.Equal("Warning",
            written.RootElement.GetProperty("Serilog").GetProperty("MinimumLevel").GetProperty("Default").GetString());
        Assert.False(written.RootElement.TryGetProperty("Zauber", out _));
        Assert.Empty(Directory.GetFiles(_contentRoot, "*.tmp"));
    }

    // "Zauber:Key" at the top of the file is legal for the JSON configuration provider. Adding a nested
    // "Zauber" object next to it would be the same key twice, which the provider refuses to load.
    [Fact]
    public void The_settings_writer_updates_flat_keys_where_they_are()
    {
        File.WriteAllText(SettingsFile,
            """{ "Zauber:ConnectionString": "", "zauber:databaseprovider": "", "Zauber": { "SiteUrl": "https://example.test" } }""");

        Assert.True(AppSettingsWriter.Write(SettingsFile, "Sqlite", "Data Source=app.db").Success);

        using var written = JsonDocument.Parse(File.ReadAllText(SettingsFile));
        Assert.Equal("Data Source=app.db", written.RootElement.GetProperty("Zauber:ConnectionString").GetString());
        Assert.Equal("Sqlite", written.RootElement.GetProperty("zauber:databaseprovider").GetString());
        var nested = written.RootElement.GetProperty("Zauber");
        Assert.False(nested.TryGetProperty("ConnectionString", out _));
        Assert.False(nested.TryGetProperty("DatabaseProvider", out _));
        // And the result is something configuration can read
        var configuration = new Microsoft.Extensions.Configuration.ConfigurationBuilder()
            .AddJsonFile(SettingsFile).Build();
        Assert.Equal("Sqlite", configuration["Zauber:DatabaseProvider"]);
        Assert.Equal("Data Source=app.db", configuration["Zauber:ConnectionString"]);
    }

    [Fact]
    public void The_settings_writer_creates_a_missing_file_and_refuses_one_that_is_not_json()
    {
        Assert.True(AppSettingsWriter.Write(SettingsFile, "Sqlite", "Data Source=app.db").Success);
        using (var created = JsonDocument.Parse(File.ReadAllText(SettingsFile)))
        {
            Assert.Equal("Sqlite", created.RootElement.GetProperty("Zauber").GetProperty("DatabaseProvider").GetString());
        }

        File.WriteAllText(SettingsFile, "this is not json");
        var refused = AppSettingsWriter.Write(SettingsFile, "Sqlite", "Data Source=app.db");

        Assert.False(refused.Success);
        Assert.Equal("this is not json", File.ReadAllText(SettingsFile));
    }
}
