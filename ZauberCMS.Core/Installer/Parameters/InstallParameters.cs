namespace ZauberCMS.Core.Installer.Parameters;

/// <summary>What the installer asks for once the site has a database.</summary>
public class InstallParameters
{
    /// <summary>The administrator's user name.</summary>
    public string? AdminName { get; set; }

    public string? AdminEmail { get; set; }

    public string? AdminPassword { get; set; }

    public string? SiteName { get; set; }

    /// <summary>Culture name of the site's default language, for example <c>en-GB</c>.</summary>
    public string? Language { get; set; } = "en-US";

    /// <summary>Whether visitors can create accounts (<c>GlobalSettings.AllowRegistration</c>).</summary>
    public bool AllowRegistration { get; set; }

    /// <summary>Full type name of the <c>IStarterKit</c> to install; empty for an empty site.</summary>
    public string? StarterKit { get; set; }
}
