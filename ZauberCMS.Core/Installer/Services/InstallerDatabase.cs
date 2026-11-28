using System.Globalization;
using System.Data.Common;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Localization;
using Npgsql;
using ZauberCMS.Core.Data;
using ZauberCMS.Core.Installer.Models;
using ZauberCMS.Core.Localization;

namespace ZauberCMS.Core.Installer.Services;

/// <summary>
/// Database work the installer does before the site has a database of its own: testing a connection and creating
/// the schema in a database that configuration does not point at yet.
/// </summary>
internal static class InstallerDatabase
{
    /// <summary>Seconds to wait for a server before telling the visitor it cannot be reached.</summary>
    private const int ConnectTimeoutSeconds = 8;

    /// <summary>
    /// Tries the connection without changing anything. A database that does not exist yet is fine as long as the
    /// server accepts the login: the migrations create it. The message is in <paramref name="text"/>'s language.
    /// </summary>
    public static async Task<DatabaseTestResult> TestAsync(string provider, string connectionString,
        IStringLocalizer<ZauberText> text, CancellationToken cancellationToken = default)
    {
        try
        {
            switch (DatabaseProviders.Normalise(provider))
            {
                case DatabaseProviders.Sqlite:
                    return await TestSqliteAsync(connectionString, text, cancellationToken);

                case DatabaseProviders.SqlServer:
                {
                    // Pooling off, as for PostgreSQL, and also because SqlClient's pool remembers a failed open: for
                    // a few seconds after "database does not exist" every open with the same string fails the same
                    // way, so a test straight after the database was created would still say it is missing.
                    var builder = new SqlConnectionStringBuilder(connectionString)
                    {
                        ConnectTimeout = ConnectTimeoutSeconds,
                        Pooling = false
                    };
                    var database = builder.InitialCatalog;
                    return await TestServerAsync(database,
                        () => new SqlConnection(builder.ConnectionString),
                        () =>
                        {
                            builder.InitialCatalog = "master";
                            return new SqlConnection(builder.ConnectionString);
                        }, text, cancellationToken);
                }

                case DatabaseProviders.PostgreSql:
                {
                    // Pooling off: a test must not leave connections to a server that may never be used
                    var builder = new NpgsqlConnectionStringBuilder(connectionString)
                    {
                        Timeout = ConnectTimeoutSeconds,
                        Pooling = false
                    };
                    var database = builder.Database ?? string.Empty;
                    return await TestServerAsync(database,
                        () => new NpgsqlConnection(builder.ConnectionString),
                        () =>
                        {
                            builder.Database = "postgres";
                            return new NpgsqlConnection(builder.ConnectionString);
                        }, text, cancellationToken);
                }

                default:
                    return new DatabaseTestResult(false, false, text["installer_chooseDatabaseType"]);
            }
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException or KeyNotFoundException)
        {
            return new DatabaseTestResult(false, false, text["installer_connectionStringInvalid", ex.Message]);
        }
    }

    /// <summary>
    /// A DbContext for <paramref name="provider"/> on <paramref name="connectionString"/>, independent of the
    /// site's configuration. The caller disposes it.
    /// </summary>
    public static ZauberDbContextBase CreateContext(string provider, string connectionString)
    {
        // The contexts read their connection string from configuration (Zauber:ConnectionString)
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{Constants.SettingsConfigName}:ConnectionString"] = connectionString
            })
            .Build();

        return DatabaseProviders.Normalise(provider) switch
        {
            DatabaseProviders.Sqlite => new SqliteZauberDbContext(
                new DbContextOptionsBuilder<SqliteZauberDbContext>().Options, configuration),
            DatabaseProviders.SqlServer => new ZauberDbContext(
                new DbContextOptionsBuilder<ZauberDbContext>().Options, configuration),
            DatabaseProviders.PostgreSql => new PostgreSqlZauberDbContext(
                new DbContextOptionsBuilder<PostgreSqlZauberDbContext>().Options, configuration),
            _ => throw new InvalidOperationException($"'{provider}' is not a database provider ZauberCMS knows.")
        };
    }

    private static async Task<DatabaseTestResult> TestSqliteAsync(string connectionString,
        IStringLocalizer<ZauberText> text, CancellationToken cancellationToken)
    {
        var builder = new SqliteConnectionStringBuilder(connectionString);
        if (string.IsNullOrWhiteSpace(builder.DataSource))
        {
            return new DatabaseTestResult(false, false, text["installer_enterSqliteFile"]);
        }

        if (builder.Mode == SqliteOpenMode.Memory || builder.DataSource == ":memory:")
        {
            // Legitimate for tests; nothing to check on disk
            return new DatabaseTestResult(true, false, text["installer_sqliteInMemory"]);
        }

        // A share or device path would make the server reach out with its own credentials on behalf of a
        // visitor who is not signed in. A database on a share is a bad idea for SQLite anyway.
        if (builder.DataSource.StartsWith(@"\\", StringComparison.Ordinal) ||
            builder.DataSource.StartsWith("//", StringComparison.Ordinal))
        {
            return new DatabaseTestResult(false, false, text["installer_sqliteOnShare"]);
        }

        var path = Path.GetFullPath(builder.DataSource);
        if (File.Exists(path))
        {
            // Read-only: a test must not turn some other file into a database
            builder.Mode = SqliteOpenMode.ReadOnly;
            builder.Cache = SqliteCacheMode.Default;
            builder.Pooling = false;
            try
            {
                await using var connection = new SqliteConnection(builder.ConnectionString);
                await connection.OpenAsync(cancellationToken);
                await using var command = connection.CreateCommand();
                command.CommandText = "SELECT count(*) FROM sqlite_master";
                await command.ExecuteScalarAsync(cancellationToken);
            }
            catch (SqliteException ex)
            {
                return new DatabaseTestResult(false, false, text["installer_sqliteNotADatabase", path, ex.Message]);
            }

            return new DatabaseTestResult(true, true, text["installer_sqliteFound", path]);
        }

        var folder = Path.GetDirectoryName(path);
        if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
        {
            return new DatabaseTestResult(false, false, text["installer_sqliteNoFolder", folder ?? string.Empty]);
        }

        try
        {
            var probe = Path.Combine(folder, $".zauber-write-test-{Guid.NewGuid():N}");
            await File.WriteAllBytesAsync(probe, [], cancellationToken);
            File.Delete(probe);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return new DatabaseTestResult(false, false, text["installer_sqliteCannotWrite", folder, ex.Message]);
        }

        return new DatabaseTestResult(true, false, text["installer_sqliteCreated", path]);
    }

    private static async Task<DatabaseTestResult> TestServerAsync(string database,
        Func<DbConnection> openDatabase, Func<DbConnection> openServer, IStringLocalizer<ZauberText> text,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(database))
        {
            return new DatabaseTestResult(false, false, text["installer_noDatabaseName"]);
        }

        DbException failure;
        try
        {
            await using var connection = openDatabase();
            await connection.OpenAsync(cancellationToken);
            return new DatabaseTestResult(true, true, text["installer_connected", database]);
        }
        catch (DbException ex)
        {
            failure = ex;
        }

        // The login may be fine and only the database missing: ask the server's own database
        try
        {
            await using var connection = openServer();
            await connection.OpenAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = connection is NpgsqlConnection
                ? "SELECT count(*) FROM pg_database WHERE datname = @name"
                : "SELECT count(*) FROM sys.databases WHERE name = @name";
            var parameter = command.CreateParameter();
            parameter.ParameterName = "@name";
            parameter.Value = database;
            command.Parameters.Add(parameter);
            var exists = Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken), CultureInfo.InvariantCulture) > 0;
            if (!exists)
            {
                return new DatabaseTestResult(true, false, text["installer_connectedNoDatabase", database]);
            }
        }
        catch (DbException)
        {
            // The first failure is the one that describes the problem
        }

        return new DatabaseTestResult(false, false, text["installer_connectFailed", failure.Message]);
    }
}
