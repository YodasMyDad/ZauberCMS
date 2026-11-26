using ZauberCMS.Core.Installer.Parameters;

namespace ZauberCMS.Core.Installer.Models;

/// <summary>
/// Everything the installer page needs to render one response. Public only because the installer's Razor
/// components are; it is not an extension point and may change between versions.
/// </summary>
public sealed class InstallerPageModel
{
    public InstallerStep Step { get; set; }

    /// <summary>Where the forms post to (the installer's own address, with the path base).</summary>
    public string FormAction { get; set; } = "/install";

    public string AntiforgeryFieldName { get; set; } = string.Empty;

    public string AntiforgeryToken { get; set; } = string.Empty;

    /// <summary>The Content-Security-Policy nonce of this response, for the inline style and script.</summary>
    public string Nonce { get; set; } = string.Empty;

    /// <summary>The ZauberCMS version, shown small.</summary>
    public string? Version { get; set; }

    public List<string> Errors { get; set; } = [];

    /// <summary>The result of "Test connection".</summary>
    public DatabaseTestResult? TestResult { get; set; }

    public DatabaseSetupParameters Database { get; set; } = new();

    /// <summary>The name of the database the site already uses; set once the database step is behind us.</summary>
    public string? ConfiguredDatabase { get; set; }

    public DatabaseSetupResult? ManualConfiguration { get; set; }

    public InstallParameters Setup { get; set; } = new();

    /// <summary>The pane of the set-up form to open first: admin, site, starter or review.</summary>
    public string StartPane { get; set; } = "admin";

    public IReadOnlyList<InstallerOption> StarterKits { get; set; } = [];

    public IReadOnlyList<InstallerOption> Languages { get; set; } = [];

    /// <summary>The password rules of this site in words.</summary>
    public string PasswordRules { get; set; } = string.Empty;

    public string? MessageTitle { get; set; }

    public List<string> MessageLines { get; set; } = [];

    /// <summary>Whether <see cref="MessageLines"/> are things that went wrong.</summary>
    public bool MessageIsProblem { get; set; }

    public string? MessageLinkText { get; set; }

    public string? MessageLinkUrl { get; set; }
}
