using System.Globalization;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using ZauberCMS.Components.Startup;
using ZauberCMS.Core.Localization;

namespace ZauberCMS.Components.Tests.Shared;

/// <summary>
/// ZA-301: Radzen asks its <c>ILocalizer</c> for its text on every render and catches nothing it throws, and the
/// bridge (D93) reads Radzen's keys through <c>GetAllStrings</c> of whatever <c>IStringLocalizer&lt;ZauberText&gt;</c>
/// the site has, its own one too (Docs/AdminUI.md "Localisation", step 6). A localiser whose <c>GetAllStrings</c>
/// throws, or gives a name twice, must leave Radzen its own English, not stop every grid and dialog of the admin.
/// </summary>
public class RadzenTextLocalizerTests
{
    private static readonly CultureInfo French = CultureInfo.GetCultureInfo("fr");

    [Fact]
    public void ZA301_a_localiser_whose_GetAllStrings_throws_leaves_Radzen_its_own_English()
    {
        var text = new SiteLocalizer(() => throw new NotImplementedException());
        var radzen = new RadzenTextLocalizer(text, NullLogger<RadzenTextLocalizer>.Instance);

        Assert.Null(radzen.Get("DataGrid_EmptyText", French));
        // Read once per culture, as before: the failure is not retried on every render
        Assert.Null(radzen.Get("DataGrid_EqualsText", French));
        Assert.Equal(1, text.Reads);
    }

    [Fact]
    public void ZA301_a_name_given_twice_is_read_once_and_the_first_one_wins()
    {
        var text = new SiteLocalizer(() =>
        [
            new LocalizedString("radzen_DataGrid_EmptyText", "Aucun enregistrement."),
            new LocalizedString("general_save", "Enregistrer"),
            new LocalizedString("radzen_DataGrid_EmptyText", "Rien (parent)")
        ]);
        var radzen = new RadzenTextLocalizer(text, NullLogger<RadzenTextLocalizer>.Instance);

        Assert.Equal("Aucun enregistrement.", radzen.Get("DataGrid_EmptyText", French));
        Assert.Null(radzen.Get("save", French));
    }

    /// <summary>A site's own localiser (texts kept in a database, say), with the <c>GetAllStrings</c> it was given.</summary>
    private sealed class SiteLocalizer(Func<IEnumerable<LocalizedString>> all) : IStringLocalizer<ZauberText>
    {
        public int Reads { get; private set; }

        public LocalizedString this[string name] => new(name, name, resourceNotFound: true);
        public LocalizedString this[string name, params object[] arguments] => this[name];

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures)
        {
            Reads++;
            return all();
        }
    }
}
