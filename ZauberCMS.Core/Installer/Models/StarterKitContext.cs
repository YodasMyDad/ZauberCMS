using ZauberCMS.Core.Languages.Models;
using ZauberCMS.Core.Membership.Models;

namespace ZauberCMS.Core.Installer.Models;

/// <summary>What the installer hands to an <see cref="Interfaces.IStarterKit"/>.</summary>
public sealed class StarterKitContext
{
    /// <summary>The site name typed into the installer.</summary>
    public required string SiteName { get; init; }

    /// <summary>
    /// The language chosen in the installer, already saved. Set it on the root page (<c>Content.LanguageId</c>) so
    /// the pages below it are in that language. Null only if saving the language failed.
    /// </summary>
    public Language? Language { get; init; }

    /// <summary>
    /// The administrator the installer created. Services called from the kit run as this user, so content saved
    /// through <c>IContentService</c> is recorded as theirs.
    /// </summary>
    public required User Administrator { get; init; }
}
