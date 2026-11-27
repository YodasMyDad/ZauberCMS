using System.Globalization;
using System.Data.Common;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Npgsql;
using ZauberCMS.Core.Data;
using ZauberCMS.Core.Installer.Models;

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
    /// server accepts the login: the migrations create it.
    /// </summary>
    public static async Task<DatabaseTestResult> TestAsync(string provider, string connectionString,
        CancellationToken cancellationToken = default)
    {
        try
        {
            switch (DatabaseProviders.Normalise(provider))
            {
                case DatabaseProviders.Sqlite:
                    return await TestSqliteAsync(connectionString, cancellationToken);

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
                        }, cancellationToken);
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
                        }, cancellationToken);
                }

                default:
                    return new DatabaseTestResult(false, false, "Choose a database type.");
            }
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException or KeyNotFoundException)
        {
            return new DatabaseTestResult(false, false, $"The connection string is not valid: {ex.Message}");
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
        CancellationToken cancellationToken)
    {
        var builder = new SqliteConnectionStringBuilder(connectionString);
        if (string.IsNullOrWhiteSpace(builder.DataSource))
        {
            return new DatabaseTestResult(false, false, "Enter a file name for the SQLite database.");
        }

        if (builder.Mode == SqliteOpenMode.Memory || builder.DataSource == ":memory:")
        {
            // Legitimate for tests; nothing to check on disk
            return new DatabaseTestResult(true, false, "An in-memory database: everything in it is lost when the site stops.");
        }

        // A share or device path would make the server reach out with its own credentials on behalf of a
        // visitor who is not signed in. A database on a share is a bad idea for SQLite anyway.
        if (builder.DataSource.StartsWith(@"\\", StringComparison.Ordinal) ||
            builder.DataSource.StartsWith("//", StringComparison.Ordinal))
        {
            return new DatabaseTestResult(false, false, "The SQLite file must be on a local disk, not on a network share.");
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
                return new DatabaseTestResult(false, false, $"{path} exists but is not a SQLite database: {ex.Message}");
            }

            return new DatabaseTestResult(true, true, $"Found the database file {path}.");
        }

        var folder = Path.GetDirectoryName(path);
        if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
        {
            return new DatabaseTestResult(false, false, $"The folder {folder} does not exist.");
        }

        try
        {
            var probe = Path.Combine(folder, $".zauber-write-test-{Guid.NewGuid():N}");
            await File.WriteAllBytesAsync(probe, [], cancellationToken);
            File.Delete(probe);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return new DatabaseTestResult(false, false, $"The site cannot write to {folder}: {ex.Message}");
        }

        return new DatabaseTestResult(true, false, $"The database file {path} is created when you continue.");
    }

    private static async Task<DatabaseTestResult> TestServerAsync(string database,
        Func<DbConnection> openDatabase, Func<DbConnection> openServer, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(database))
        {
            return new DatabaseTestResult(false, false, "The connection string does not name a database.");
        }

        DbException failure;
        try
        {
            await using var connection = openDatabase();
            await connection.OpenAsync(cancellationToken);
            return new DatabaseTestResult(true, true, $"Connected to the database \"{database}\".");
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
                return new DatabaseTestResult(true, false,
                    $"Connected to the server. The database \"{database}\" does not exist yet and is created when " +
                    "you continue; the login needs permission to create databases.");
            }
        }
        catch (DbException)
        {
            // The first failure is the one that describes the problem
        }

        return new DatabaseTestResult(false, false, $"Could not connect: {failure.Message}");
    }
}
