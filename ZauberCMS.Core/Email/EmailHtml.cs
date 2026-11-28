using System.Globalization;
using System.Text.Encodings.Web;
using Microsoft.Extensions.Localization;

namespace ZauberCMS.Core.Email;

/// <summary>ZauberCMS's text in the HTML of an email (Roadmap Phase 10h-4).</summary>
internal static class EmailHtml
{
    /// <summary>
    /// A sentence of ZauberCMS's text as HTML: the sentence encoded (a translation is text, not markup), then
    /// <paramref name="html"/> (a link's opening and closing tags, built and encoded by the caller) put in its
    /// placeholders, so the sentence stays whole for the translator.
    /// </summary>
    internal static string Paragraph(LocalizedString sentence, params string[] html)
    {
        var encoded = HtmlEncoder.Default.Encode(sentence.Value);
        if (html.Length == 0)
        {
            return encoded;
        }

        try
        {
            return string.Format(CultureInfo.CurrentCulture, encoded, html);
        }
        catch (FormatException)
        {
            // A translation whose placeholders do not match: shown unformatted, as the localiser does
            return encoded;
        }
    }
}
