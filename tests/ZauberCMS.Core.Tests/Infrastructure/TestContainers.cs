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
