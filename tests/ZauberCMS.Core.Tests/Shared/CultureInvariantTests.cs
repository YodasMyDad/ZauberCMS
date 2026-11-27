using System.Globalization;
using ZauberCMS.Core.Extensions;

namespace ZauberCMS.Core.Tests.Shared;

/// <summary>
/// Values ZauberCMS writes or parses for itself do not depend on the server's culture (found by the .NET
/// globalisation analysers switched on in Roadmap Phase 11b, D43).
/// </summary>
public class CultureInvariantTests
{
    [Fact]
    public void ZA131_SplitStringUsing_parses_numbers_the_same_in_every_culture()
    {
        var values = InCulture("de-DE", () => StringExtensions.SplitStringUsing<decimal>("1.5|2.25", "|").ToList());

        // In de-DE "." is the group separator, so the culture's parse read "1.5" as 15.
        Assert.Equal([1.5m, 2.25m], values);
    }

    [Fact]
    public void ZA131_EnsureNotStartsWith_and_EnsureNotEndsWith_compare_the_character_exactly()
    {
        Assert.Equal("path", InCulture("en-GB", () => "/path/".EnsureNotStartsWith('/').EnsureNotEndsWith('/')));

        // A soft hyphen is ignored by a culture comparison, so every string "started" and "ended" with it: the first
        // letter was removed, and the end check threw (the character is not there to remove).
        Assert.Equal("path", InCulture("en-GB", () => "path".EnsureNotStartsWith('­')));
        Assert.Equal("path", InCulture("en-GB", () => "path".EnsureNotEndsWith('­')));
    }

    private static T InCulture<T>(string name, Func<T> action)
    {
        var culture = CultureInfo.CurrentCulture;
        var uiCulture = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(name);
            return action();
        }
        finally
        {
            CultureInfo.CurrentCulture = culture;
            CultureInfo.CurrentUICulture = uiCulture;
        }
    }
}
