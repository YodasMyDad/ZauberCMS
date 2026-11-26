namespace ZauberCMS.Core.Installer.Models;

/// <summary>Which installer screen is shown.</summary>
public enum InstallerStep
{
    /// <summary>Choose and create the database.</summary>
    Database,

    /// <summary>The database is ready but configuration has to be changed by hand.</summary>
    ManualConfiguration,

    /// <summary>Administrator, site, starter content and review, in one form.</summary>
    Setup,

    /// <summary>A message with nothing to fill in: a problem, or an install that finished with warnings.</summary>
    Message
}
