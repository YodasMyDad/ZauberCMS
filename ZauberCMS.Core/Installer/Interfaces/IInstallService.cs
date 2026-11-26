using ZauberCMS.Core.Installer.Models;
using ZauberCMS.Core.Installer.Parameters;

namespace ZauberCMS.Core.Installer.Interfaces;

/// <summary>
/// The work behind the installer pages. The pages themselves are served by <c>InstallerMiddleware</c> while the
/// site is not installed. The methods that change anything refuse a site that is already set up.
/// </summary>
public interface IInstallService
{
    /// <summary>
    /// Brings the configured database up to date (migrations, then every <c>ISeedData</c>), works out whether
    /// the site is installed and applies <c>Zauber:Identity:RecoverAdminUser</c>. Runs at start-up, and again
    /// when the installer has given the site a database.
    /// </summary>
    Task InitialiseDatabaseAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Whether the site is installed. Reads the flag from the database until it is found (another server may
    /// have finished the install); a database that already has users is marked installed here.
    /// </summary>
    Task<bool> CheckInstalledAsync(CancellationToken cancellationToken = default);

    /// <summary>Tries a database connection without changing anything.</summary>
    Task<DatabaseTestResult> TestDatabaseAsync(DatabaseSetupParameters parameters,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates the tables in the chosen database, writes the choice into <c>appsettings.json</c> and switches
    /// the running site to it. Only while the site has no database.
    /// </summary>
    Task<DatabaseSetupResult> SetUpDatabaseAsync(DatabaseSetupParameters parameters,
        CancellationToken cancellationToken = default);

    /// <summary>The starter kits found in the scanned assemblies, by full type name.</summary>
    IReadOnlyDictionary<string, IStarterKit> GetStarterKits();

    /// <summary>
    /// Creates the administrator, marks the site installed, then saves the language and settings and runs the
    /// chosen starter kit. Only while the site is not installed.
    /// </summary>
    Task<InstallResult> InstallAsync(InstallParameters parameters, CancellationToken cancellationToken = default);
}
