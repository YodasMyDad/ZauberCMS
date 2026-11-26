using ZauberCMS.Core.Installer.Models;

namespace ZauberCMS.Core.Installer.Interfaces;

/// <summary>
/// Starter content the installer offers: content types, pages and whatever else gives a new site something to
/// look at. Found by discovery: add a public class that implements this to the site's project (the
/// <c>dotnet new zaubercms</c> template has one) or ship it in a package together with its views.
/// Instances are created with constructor injection, so ask for <c>IContentService</c> and the like there.
/// </summary>
public interface IStarterKit
{
    /// <summary>Shown as the title of the choice in the installer.</summary>
    string Name { get; }

    /// <summary>One or two sentences under the title: what the site gets.</summary>
    string Description { get; }

    /// <summary>
    /// Creates the content. It runs once, after the administrator exists and the site is marked installed; an
    /// exception is logged and shown to the administrator but does not undo the install.
    /// </summary>
    Task InstallAsync(StarterKitContext context, CancellationToken cancellationToken = default);
}
