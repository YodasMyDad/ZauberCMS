using Npgsql;
using Testcontainers.PostgreSql;
using Testcontainers.Redis;

namespace ZauberCMS.Core.Tests.Infrastructure;

/// <summary>
/// One PostgreSQL and one Redis container for the whole test run, started on first use (Testcontainers; the
/// containers are removed when the test process ends). Without Docker the tests that need them are skipped with a
/// clear message — except on CI (the <c>CI</c> environment variable is set), where a missing container fails the
/// run, so CI can never pass by skipping them.
/// </summary>
public static class TestContainers
{
    private static readonly Lazy<Task<PostgreSqlContainer>> PostgreSql = new(async () =>
    {
        // Every test site has its own database and connection pool; leave room for many at once
        var container = new PostgreSqlBuilder("postgres:17-alpine")
            .WithCommand("-c", "max_connections=300")
            .Build();
        await container.StartAsync();
        return container;
    });

    private static readonly Lazy<Task<RedisContainer>> Redis = new(async () =>
    {
        var container = new RedisBuilder("redis:7-alpine").Build();
        await container.StartAsync();
        return container;
    });

    /// <summary>Creates an empty database in the shared PostgreSQL container and returns its connection string.</summary>
    public static async Task<string> CreatePostgreSqlDatabaseAsync()
    {
        var container = await StartOrSkipAsync(PostgreSql, "PostgreSQL");
        var databaseName = $"zauber_{Guid.NewGuid():N}";

        await using (var connection = new NpgsqlConnection(container.GetConnectionString()))
        {
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand($"CREATE DATABASE \"{databaseName}\"", connection);
            await command.ExecuteNonQueryAsync();
        }

        return new NpgsqlConnectionStringBuilder(container.GetConnectionString()) { Database = databaseName }
            .ConnectionString;
    }

    /// <summary>Closes the pooled connections to a test database and drops it.</summary>
    public static async Task DropPostgreSqlDatabaseAsync(string connectionString)
    {
        await using (var pooled = new NpgsqlConnection(connectionString))
        {
            NpgsqlConnection.ClearPool(pooled);
        }

        var container = await PostgreSql.Value;
        var databaseName = new NpgsqlConnectionStringBuilder(connectionString).Database;
        await using var connection = new NpgsqlConnection(container.GetConnectionString());
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{databaseName}\" WITH (FORCE)", connection);
        await command.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// SQL Server is not run in a container: set <c>ZAUBER_TEST_SQLSERVER</c> to a connection string without a
    /// database (for example <c>Server=myserver;Integrated Security=true;TrustServerCertificate=true</c>); the login
    /// needs CREATE DATABASE. Without it the SQL Server tests are skipped, also on CI.
    /// </summary>
    public static async Task<string> CreateSqlServerDatabaseAsync()
    {
        var server = Environment.GetEnvironmentVariable("ZAUBER_TEST_SQLSERVER");
        if (string.IsNullOrWhiteSpace(server))
        {
            Assert.Skip("SQL Server tests need ZAUBER_TEST_SQLSERVER (a connection string to a server where the " +
                        "login may create databases).");
        }

        var databaseName = $"zauber_test_{Guid.NewGuid():N}";
        await using (var connection = new Microsoft.Data.SqlClient.SqlConnection(server))
        {
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = $"CREATE DATABASE [{databaseName}]";
            await command.ExecuteNonQueryAsync();
        }

        return new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(server) { InitialCatalog = databaseName }
            .ConnectionString;
    }

    /// <summary>Closes the pooled connections to a SQL Server test database and drops it.</summary>
    public static async Task DropSqlServerDatabaseAsync(string connectionString)
    {
        var builder = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(connectionString);
        var databaseName = builder.InitialCatalog;
        await using (var pooled = new Microsoft.Data.SqlClient.SqlConnection(connectionString))
        {
            Microsoft.Data.SqlClient.SqlConnection.ClearPool(pooled);
        }

        builder.InitialCatalog = "master";
        await using var connection = new Microsoft.Data.SqlClient.SqlConnection(builder.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"""
                               IF DB_ID(N'{databaseName}') IS NOT NULL
                               BEGIN
                                   ALTER DATABASE [{databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
                                   DROP DATABASE [{databaseName}];
                               END
                               """;
        await command.ExecuteNonQueryAsync();
    }

    /// <summary>The connection string of the shared Redis container.</summary>
    public static async Task<string> RedisConnectionStringAsync()
    {
        var container = await StartOrSkipAsync(Redis, "Redis");
        return container.GetConnectionString();
    }

    private static async Task<T> StartOrSkipAsync<T>(Lazy<Task<T>> container, string name)
    {
        try
        {
            return await container.Value;
        }
        catch (Exception ex) when (Environment.GetEnvironmentVariable("CI") == null)
        {
            Assert.Skip($"{name} tests need Docker, which is not available here ({ex.GetType().Name}: {ex.Message}). " +
                        "Start Docker to run them; CI always runs them.");
            throw; // not reached: Assert.Skip throws
        }
    }
}
