using ZauberCMS.Core.Membership.Models;

namespace ZauberCMS.Core.Installer.Models;

/// <summary>The outcome of <see cref="Interfaces.IInstallService.InstallAsync"/>.</summary>
public sealed class InstallResult
{
    /// <summary>The administrator exists and the site is marked installed.</summary>
    public bool Success { get; set; }

    /// <summary>The site was installed before this call; nothing was changed.</summary>
    public bool AlreadyInstalled { get; set; }

    /// <summary>Why the install did not happen. Written for the person filling in the form.</summary>
    public List<string> Errors { get; } = [];

    /// <summary>
    /// The part of the installer form the first error belongs to: <c>admin</c>, <c>site</c> or <c>starter</c>.
    /// The page opens there.
    /// </summary>
    public string? ErrorPart { get; set; }

    /// <summary>
    /// Things that went wrong after the site was installed (the language, the settings or the starter content).
    /// The install stands; these are shown once and logged.
    /// </summary>
    public List<string> Warnings { get; } = [];

    /// <summary>The administrator that was created, for signing them in.</summary>
    public User? Administrator { get; set; }
}
