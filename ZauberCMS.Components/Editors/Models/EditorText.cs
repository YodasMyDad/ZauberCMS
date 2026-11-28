using System.Collections;
using System.Globalization;
using System.Resources;
using Microsoft.Extensions.Localization;
using ZauberCMS.Core.Localization;

namespace ZauberCMS.Components.Editors.Models;

/// <summary>
/// ZauberCMS's English text of the property editors, for an editor made with <c>new</c> (a starter kit that reads
/// its alias and type): discovery and Blazor build the editors with the site's localiser instead (Roadmap D120).
/// Reads ZauberCMS's own <c>Editors.resx</c>, so a site's or language pack's files do not apply to it.
/// </summary>
internal sealed class EditorText : IStringLocalizer<ZauberText>
{
    /// <summary>The one instance the editors' parameterless constructors use.</summary>
    public static readonly IStringLocalizer<ZauberText> English = new EditorText();

    // The manifest resource of Resources/ZauberText/Editors.resx in this assembly
    private static readonly ResourceManager Resources =
        new("ZauberCMS.Components.Resources.ZauberText.Editors", typeof(EditorText).Assembly);

    private EditorText()
    {
    }

    public LocalizedString this[string name]
    {
        get
        {
            var value = Resources.GetString(name, CultureInfo.InvariantCulture);
            return new LocalizedString(name, value ?? name, resourceNotFound: value == null);
        }
    }

    public LocalizedString this[string name, params object[] arguments]
    {
        get
        {
            var text = this[name];
            return text.ResourceNotFound
                ? text
                : new LocalizedString(name, string.Format(CultureInfo.CurrentCulture, text.Value, arguments));
        }
    }

    public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) =>
        Resources.GetResourceSet(CultureInfo.InvariantCulture, createIfNotExists: true, tryParents: false)?
            .Cast<DictionaryEntry>()
            .Select(e => new LocalizedString((string)e.Key, e.Value as string ?? string.Empty))
            .ToList() ?? [];
}
