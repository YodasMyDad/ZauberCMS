using System.Globalization;
using System.Net;
using Microsoft.AspNetCore.Components;

namespace ZauberCMS.Core.Installer.Components;

/// <summary>
/// A localised sentence that names a file, a setting or a command: the sentence is one text with placeholders
/// (Docs/AdminUI.md "Localisation", rule 4) and each name is shown as code. Both are HTML-encoded, so a translation
/// cannot add markup.
/// </summary>
internal static class InstallerMarkup
{
    /// <param name="sentence">The text, with <c>{0}</c>, <c>{1}</c>… where the names go (<c>L["installer_…"]</c>).</param>
    /// <param name="code">The names, in placeholder order.</param>
    public static MarkupString WithCode(string sentence, params string[] code)
    {
        var encoded = WebUtility.HtmlEncode(sentence);
        try
        {
            return new MarkupString(string.Format(CultureInfo.CurrentCulture, encoded,
                code.Select(object (c) => $"<code>{WebUtility.HtmlEncode(c)}</code>").ToArray()));
        }
        catch (FormatException)
        {
            // A translation with a placeholder the code does not pass: shown unformatted, as the localiser does
            return new MarkupString(encoded);
        }
    }
}
