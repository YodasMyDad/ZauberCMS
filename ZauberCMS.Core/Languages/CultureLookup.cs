using System.Globalization;

namespace ZauberCMS.Core.Languages;

/// <summary>Turns a culture name that somebody typed or posted into a culture the server really has.</summary>
public static class CultureLookup
{
    /// <summary>
    /// The specific culture (language and region, for example <c>en-GB</c>) with this name, or <c>null</c> when the
    /// name is blank, is not one .NET knows on this server, or names a language without a region.
    /// </summary>
    public static CultureInfo? FindSpecific(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        try
        {
            var culture = CultureInfo.GetCultureInfo(name.Trim(), predefinedOnly: true);
            return culture.IsNeutralCulture || string.IsNullOrEmpty(culture.Name) ? null : culture;
        }
        catch (CultureNotFoundException)
        {
            return null;
        }
    }
}
