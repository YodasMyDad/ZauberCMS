namespace ZauberCMS.Core.Installer.Models;

/// <summary>
/// The record that says a site is installed. It is stored as a <c>GlobalData</c> row with the alias
/// <see cref="Alias"/>; the row existing is what counts, the content is for people reading the database.
/// </summary>
public class InstallFlag
{
    /// <summary>The <c>GlobalData.Alias</c> of the row.</summary>
    public const string Alias = "ZauberInstalled";

    /// <summary><see cref="Source"/> when the installer finished.</summary>
    public const string Installer = "Installer";

    /// <summary><see cref="Source"/> when a site that already had users was started on a version with the installer.</summary>
    public const string Upgrade = "Upgrade";

    public DateTime InstalledOn { get; set; } = DateTime.UtcNow;

    /// <summary><see cref="Installer"/> or <see cref="Upgrade"/>.</summary>
    public string? Source { get; set; }

    /// <summary>The ZauberCMS.Core version that wrote the row.</summary>
    public string? Version { get; set; }
}
