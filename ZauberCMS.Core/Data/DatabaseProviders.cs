namespace ZauberCMS.Core.Data;

/// <summary>The database providers ZauberCMS ships a DbContext and migrations for.</summary>
public static class DatabaseProviders
{
    public const string Sqlite = "Sqlite";
    public const string SqlServer = "SqlServer";
    public const string PostgreSql = "PostgreSql";

    public static readonly IReadOnlyList<string> All = [Sqlite, SqlServer, PostgreSql];

    /// <summary>
    /// The provider name in its canonical spelling (<c>Zauber:DatabaseProvider</c> is not case-sensitive), or
    /// <c>null</c> when <paramref name="value"/> is blank or not a known provider.
    /// </summary>
    public static string? Normalise(string? value)
    {
        var trimmed = value?.Trim();
        return All.FirstOrDefault(p => string.Equals(p, trimmed, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>The DbContext type that serves <paramref name="provider"/>.</summary>
    /// <exception cref="InvalidOperationException">The provider is not one of <see cref="All"/>.</exception>
    public static Type ContextType(string? provider) => Normalise(provider) switch
    {
        Sqlite => typeof(SqliteZauberDbContext),
        SqlServer => typeof(ZauberDbContext),
        PostgreSql => typeof(PostgreSqlZauberDbContext),
        _ => throw new InvalidOperationException(
            $"'{provider}' is not a database provider ZauberCMS knows. Use one of: {string.Join(", ", All)}.")
    };
}
