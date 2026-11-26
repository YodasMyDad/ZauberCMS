namespace ZauberCMS.Core.Installer.Models;

/// <summary>The outcome of <see cref="Interfaces.IInstallService.SetUpDatabaseAsync"/>.</summary>
public sealed class DatabaseSetupResult
{
    /// <summary>The database has its tables and the site is using it.</summary>
    public bool Success { get; set; }

    /// <summary>Why not. Written for the person filling in the form.</summary>
    public List<string> Errors { get; } = [];

    /// <summary>
    /// The database is ready but the site's configuration could not be changed (or something overrides it): the
    /// owner has to set <see cref="DatabaseProvider"/> and <see cref="ConnectionString"/> by hand.
    /// </summary>
    public bool ManualConfigurationRequired { get; set; }

    /// <summary>Why the configuration could not be written, when <see cref="ManualConfigurationRequired"/>.</summary>
    public string? ManualConfigurationReason { get; set; }

    /// <summary>
    /// <c>appsettings.json</c> now holds the two values. When this is true and
    /// <see cref="ManualConfigurationRequired"/> too, something else overrides the file.
    /// </summary>
    public bool SettingsFileWritten { get; set; }

    public string? DatabaseProvider { get; set; }

    public string? ConnectionString { get; set; }
}
