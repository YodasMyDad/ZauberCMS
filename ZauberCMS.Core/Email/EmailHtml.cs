using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Unicode;
using Microsoft.Extensions.Localization;
using Serilog;
using ZauberCMS.Core.Localization;

namespace ZauberCMS.Core.Email;

/// <summary>
/// ZauberCMS's text in HTML: an email's paragraphs (Roadmap Phase 10h-4, D132) and the one account-page message
/// with a link (the reset password's, ZA-304).
/// </summary>
internal static class EmailHtml
{
    // Encodes what HTML needs (<, >, &, quotes) and leaves the letters of every language as they are:
    // HtmlEncoder.Default writes every letter outside Basic Latin as an entity ("best&#xE4;tigen", ZA-304)
    private static readonly HtmlEncoder Encoder = HtmlEncoder.Create(UnicodeRanges.All);

    /// <summary>
    /// A sentence of ZauberCMS's text as HTML: the sentence encoded (a translation is text, not markup), then
    /// <paramref name="html"/> (a link's opening and closing tags, built and encoded by the caller) put in its
    /// placeholders, so the sentence stays whole for the translator. A translation whose placeholders do not
    /// match is logged and Core's English is used, so the link is never lost (ZA-304).
    /// </summary>
    internal static string Paragraph(LocalizedString sentence, params string[] html)
    {
        var encoded = Encoder.Encode(sentence.Value);
        if (html.Length == 0)
        {
            return encoded;
        }

        if (TryFormat(encoded, html) is { } formatted)
        {
            return formatted;
        }

        Log.Warning("The ZauberCMS text {Key} in {Culture} does not match its {Count} placeholder(s), so the email " +
                    "or message uses its English", sentence.Name, CultureInfo.CurrentUICulture.Name, html.Length);
        var english = CoreText.English[sentence.Name];
        return !english.ResourceNotFound && TryFormat(Encoder.Encode(english.Value), html) is { } fallback
            ? fallback
            : encoded;
    }

    // Null when a placeholder is missing (a link left open) or the sentence does not format ({2}, a lone brace)
    private static string? TryFormat(string encoded, string[] html)
    {
        for (var i = 0; i < html.Length; i++)
        {
            if (!encoded.Contains("{" + i.ToString(CultureInfo.InvariantCulture) + "}", StringComparison.Ordinal))
            {
                return null;
            }
        }

        try
        {
            return string.Format(CultureInfo.CurrentCulture, encoded, html);
        }
        catch (FormatException)
        {
            return null;
        }
    }
}
