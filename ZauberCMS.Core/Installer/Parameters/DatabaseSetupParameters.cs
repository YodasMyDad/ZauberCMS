using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using Npgsql;
using ZauberCMS.Core.Data;

namespace ZauberCMS.Core.Installer.Parameters;

/// <summary>What the installer's database step asks for.</summary>
public class DatabaseSetupParameters
{
    /// <summary>One of <see cref="DatabaseProviders.All"/>.</summary>
    public string Provider { get; set; } = DatabaseProviders.Sqlite;

    /// <summary>Use <see cref="ConnectionString"/> as typed instead of building one from the fields below.</summary>
    public bool UseConnectionString { get; set; }

    public string? ConnectionString { get; set; }

    /// <summary>SQLite: the database file, relative to the folder the site runs in or a full path.</summary>
    public string? SqliteFile { get; set; } = "app.db";

    /// <summary>SQL Server: server name (and instance or port); PostgreSQL: host.</summary>
    public string? Server { get; set; }

    /// <summary>PostgreSQL only. SQL Server takes a port as part of <see cref="Server"/> (<c>host,1433</c>).</summary>
    public int? Port { get; set; }

    public string? Database { get; set; }

    /// <summary>SQL Server only: sign in as the account the site runs under.</summary>
    public bool IntegratedSecurity { get; set; }

    public string? Username { get; set; }

    public string? Password { get; set; }

    /// <summary>SQL Server only: accept a certificate that is not from a trusted authority (local and test servers).</summary>
    public bool TrustServerCertificate { get; set; }

    /// <summary>
    /// The connection string for these values. The provider's own builder does the quoting, so a value cannot add
    /// keywords of its own.
    /// </summary>
    /// <exception cref="ArgumentException">A required value is missing or the connection string cannot be read.
    /// The message is written for the person filling in the form.</exception>
    public string BuildConnectionString()
    {
        var provider = DatabaseProviders.Normalise(Provider)
                       ?? throw new ArgumentException("Choose a database type.");

        if (UseConnectionString)
        {
            if (string.IsNullOrWhiteSpace(ConnectionString))
            {
                throw new ArgumentException("Enter a connection string.");
            }

            var connectionString = ConnectionString.Trim();
            try
            {
                // Parsing rejects unknown keywords before anything is written to appsettings.json
                _ = provider switch
                {
                    DatabaseProviders.Sqlite => new SqliteConnectionStringBuilder(connectionString).ConnectionString,
                    DatabaseProviders.SqlServer => new SqlConnectionStringBuilder(connectionString).ConnectionString,
                    _ => new NpgsqlConnectionStringBuilder(connectionString).ConnectionString
                };
            }
            catch (Exception ex) when (ex is ArgumentException or FormatException or KeyNotFoundException)
            {
                throw new ArgumentException($"The connection string is not valid for {DisplayName(provider)}: {ex.Message}");
            }

            return connectionString;
        }

        switch (provider)
        {
            case DatabaseProviders.Sqlite:
                if (string.IsNullOrWhiteSpace(SqliteFile))
                {
                    throw new ArgumentException("Enter a file name for the SQLite database.");
                }

                return new SqliteConnectionStringBuilder
                {
                    DataSource = SqliteFile.Trim(),
                    Cache = SqliteCacheMode.Shared
                }.ConnectionString;

            case DatabaseProviders.SqlServer:
            {
                RequireServerAndDatabase();
                var builder = new SqlConnectionStringBuilder
                {
                    DataSource = Server!.Trim(),
                    InitialCatalog = Database!.Trim(),
                    TrustServerCertificate = TrustServerCertificate
                };
                if (IntegratedSecurity)
                {
                    builder.IntegratedSecurity = true;
                }
                else
                {
                    RequireUsername();
                    builder.UserID = Username!.Trim();
                    builder.Password = Password ?? string.Empty;
                }

                return builder.ConnectionString;
            }

            default:
            {
                RequireServerAndDatabase();
                RequireUsername();
                var builder = new NpgsqlConnectionStringBuilder
                {
                    Host = Server!.Trim(),
                    Database = Database!.Trim(),
                    Username = Username!.Trim(),
                    Password = Password ?? string.Empty
                };
                if (Port is > 0 and <= 65535)
                {
                    builder.Port = Port.Value;
                }
                else if (Port != null)
                {
                    throw new ArgumentException("The port must be a number between 1 and 65535.");
                }

                return builder.ConnectionString;
            }
        }
    }

    /// <summary>The name shown to people for <paramref name="provider"/>.</summary>
    public static string DisplayName(string? provider) => DatabaseProviders.Normalise(provider) switch
    {
        DatabaseProviders.Sqlite => "SQLite",
        DatabaseProviders.SqlServer => "SQL Server",
        DatabaseProviders.PostgreSql => "PostgreSQL",
        _ => provider ?? string.Empty
    };

    private void RequireServerAndDatabase()
    {
        if (string.IsNullOrWhiteSpace(Server))
        {
            throw new ArgumentException("Enter the server the database is on.");
        }

        if (string.IsNullOrWhiteSpace(Database))
        {
            throw new ArgumentException("Enter the name of the database.");
        }
    }

    private void RequireUsername()
    {
        if (string.IsNullOrWhiteSpace(Username))
        {
            throw new ArgumentException("Enter the user name the site signs in to the database with.");
        }
    }
}
