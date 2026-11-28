using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;

namespace ZauberCMS.Core.Localization;

/// <summary>
/// Core's own English text, for code that has no localiser from DI (an <c>ExtensionManager</c> built over a
/// provider without ZauberCMS's text, ZA-302) or must fall back to the English (a translated email sentence whose
/// placeholders do not match, ZA-304). It follows the current UI culture, but Core ships no translations.
/// </summary>
internal static class CoreText
{
    /// <summary>The one instance: Core's <c>Resources/ZauberText</c> files, read once.</summary>
    internal static readonly IStringLocalizer<ZauberText> English = new ZauberTextLocalizer(
        ZauberTextCatalog.FromAssemblies([], [typeof(CoreText).Assembly], NullLoggerFactory.Instance),
        NullLogger<ZauberTextLocalizer>.Instance);
}
