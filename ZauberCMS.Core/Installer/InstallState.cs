using Microsoft.Extensions.Configuration;
using ZauberCMS.Core.Data;

namespace ZauberCMS.Core.Installer;

/// <summary>
/// Whether this site has a database and has been installed. One instance per application.
/// <para>
/// The database part is read from configuration every time, so it follows <c>appsettings.json</c> when the installer
/// (or the site owner) writes it. "Installed" is a flag stored in the database (<see cref="Models.InstallFlag"/>); it is read
/// at start-up and after each installer step and, once true, stays true for the life of the process: the
/// installer never reopens.
/// </para>
/// </summary>
public sealed class InstallState(IConfiguration configuration)
{
    private volatile bool _installed;
    private volatile bool _databaseInitialised;
    private volatile string? _activeProvider;

    /// <summary>
    /// The configured provider in its canonical spelling, or <c>null</c> when <c>Zauber:DatabaseProvider</c> is
    /// blank or unknown. A missing key means SQLite, as it always has.
    /// <para>
    /// Once the site is working on a database the provider is fixed until the process ends: the services
    /// registered at start-up belong to it, so a later edit of the key takes effect on the next start, as it
    /// always has. Until then the value follows configuration, so a mistake made while setting the database up
    /// can be corrected without a restart.
    /// </para>
    /// </summary>
    public string? DatabaseProvider
    {
        get
        {
            if (_activeProvider != null)
            {
                return _activeProvider;
            }

            var value = configuration.GetSection(Constants.SettingsConfigName)["DatabaseProvider"];
            return value == null ? DatabaseProviders.Sqlite : DatabaseProviders.Normalise(value);
        }
    }

    /// <summary>The configured connection string, or <c>null</c> when it is blank.</summary>
    public string? ConnectionString
    {
        get
        {
            var value = configuration.GetSection(Constants.SettingsConfigName)["ConnectionString"];
            return string.IsNullOrWhiteSpace(value) ? null : value;
        }
    }

    /// <summary>
    /// True when configuration names a provider and a connection string. While false, nothing in the application
    /// can use the database and every request is sent to the installer's database step.
    /// </summary>
    public bool IsDatabaseConfigured => DatabaseProvider != null && ConnectionString != null;

    /// <summary>
    /// True once this process has brought the database up to date (migrations and seed data). False on a site
    /// whose database appeared after start-up, until the installer has prepared it.
    /// </summary>
    public bool IsDatabaseInitialised => _databaseInitialised;

    /// <summary>True once the site has been installed (see <see cref="Models.InstallFlag"/>).</summary>
    public bool IsInstalled => _installed;

    // One at a time per application: the migrations and seeders, and the installer's own steps (two people
    // must not both become the first administrator)
    internal SemaphoreSlim InitialiseGate { get; } = new(1, 1);

    internal SemaphoreSlim InstallGate { get; } = new(1, 1);

    /// <summary>Keeps the provider that is configured now for the rest of the process.</summary>
    internal void FixProvider() => _activeProvider ??= DatabaseProvider;

    /// <summary>Records that migrations and seed data have run in this process, on the provider in use.</summary>
    internal void MarkDatabaseInitialised()
    {
        FixProvider();
        _databaseInitialised = true;
    }

    /// <summary>Records that the database holds the installed flag. There is no way back.</summary>
    internal void MarkInstalled() => _installed = true;
}
