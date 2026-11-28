using System.Globalization;
using Microsoft.Extensions.Localization;
using ZauberCMS.Core.Email;

namespace ZauberCMS.Core.Tests.Localization;

/// <summary>
/// ZA-304: a sentence of ZauberCMS's text in HTML (an email's paragraph, D132; the reset password's message on the
/// account page) is encoded text with its link's tags as placeholders. A translation keeps its letters, can't add
/// markup, and a translation whose placeholders do not match falls back to Core's English instead of losing the
/// link (a site's or language pack's file: the guard only reads ZauberCMS's own).
/// </summary>
public class EmailHtmlTests
{
    private const string Open = "<a href='https://example.test/confirm?code=a&amp;b'>";
    private const string Close = "</a>";

    [Fact]
    public void ZA304_a_translated_sentence_keeps_its_letters_and_cannot_add_markup()
    {
        var html = EmailHtml.Paragraph(new LocalizedString("email_confirmAccountBody",
            "Bitte bestätigen Sie Ihr Konto, indem Sie {0}hier klicken{1}. <b>Привет</b> & 'ok'"), Open, Close);

        Assert.Contains("bestätigen", html, StringComparison.Ordinal);
        Assert.Contains("Привет", html, StringComparison.Ordinal);
        Assert.Contains(Open + "hier klicken" + Close, html, StringComparison.Ordinal);
        Assert.Contains("&lt;b&gt;", html, StringComparison.Ordinal);
        Assert.Contains("&amp;", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<b>", html, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Cliquez {0}ici{2}")] // a placeholder the code does not pass
    [InlineData("Cliquez {0}ici")] // the link would be left open
    [InlineData("Cliquez {0}ici{1} {")] // a lone brace
    public void ZA304_a_translation_whose_placeholders_do_not_match_uses_the_English_and_keeps_the_link(string translation)
    {
        var previous = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("fr");

            var html = EmailHtml.Paragraph(new LocalizedString("email_confirmAccountBody", translation), Open, Close);

            Assert.Equal("Please confirm your account by " + Open + "clicking here" + Close + ".", html);
        }
        finally
        {
            CultureInfo.CurrentUICulture = previous;
        }
    }

    [Fact]
    public void ZA304_a_sentence_with_no_link_is_only_encoded()
    {
        Assert.Equal("If it was not you, ignore this &lt;email&gt;.",
            EmailHtml.Paragraph(new LocalizedString("email_changeEmailIgnore", "If it was not you, ignore this <email>.")));
    }
}
