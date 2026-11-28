using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Localization;
using Npgsql;
using ZauberCMS.Core.Data;
using ZauberCMS.Core.Localization;

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
    /// <param name="text">ZauberCMS's text (<c>IStringLocalizer&lt;ZauberText&gt;</c>): the exception's message is
    /// in its language.</param>
    /// <exception cref="ArgumentException">A required value is missing or the connection string cannot be read.
    /// The message is written for the person filling in the form, so unlike other exceptions it is localised
    /// (Roadmap D121).</exception>
    public string BuildConnectionString(IStringLocalizer<ZauberText> text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var provider = DatabaseProviders.Normalise(Provider)
                       ?? throw new ArgumentException(text["installer_chooseDatabaseType"]);

        if (UseConnectionString)
        {
            if (string.IsNullOrWhiteSpace(ConnectionString))
            {
                throw new ArgumentException(text["installer_enterConnectionString"]);
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
                throw new ArgumentException(text["installer_connectionStringInvalidFor", DisplayName(provider), ex.Message]);
            }

            return connectionString;
        }

        switch (provider)
        {
            case DatabaseProviders.Sqlite:
                if (string.IsNullOrWhiteSpace(SqliteFile))
                {
                    throw new ArgumentException(text["installer_enterSqliteFile"]);
                }

                return new SqliteConnectionStringBuilder
                {
                    DataSource = SqliteFile.Trim(),
                    Cache = SqliteCacheMode.Shared
                }.ConnectionString;

            case DatabaseProviders.SqlServer:
            {
                RequireServerAndDatabase(text);
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
                    RequireUsername(text);
                    builder.UserID = Username!.Trim();
                    builder.Password = Password ?? string.Empty;
                }

                return builder.ConnectionString;
            }

            default:
            {
                RequireServerAndDatabase(text);
                RequireUsername(text);
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
                    throw new ArgumentException(text["installer_portOutOfRange", 1, 65535]);
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

    private void RequireServerAndDatabase(IStringLocalizer<ZauberText> text)
    {
        if (string.IsNullOrWhiteSpace(Server))
        {
            throw new ArgumentException(text["installer_enterServer"]);
        }

        if (string.IsNullOrWhiteSpace(Database))
        {
            throw new ArgumentException(text["installer_enterDatabaseName"]);
        }
    }

    private void RequireUsername(IStringLocalizer<ZauberText> text)
    {
        if (string.IsNullOrWhiteSpace(Username))
        {
            throw new ArgumentException(text["installer_enterDatabaseUser"]);
        }
    }
}
