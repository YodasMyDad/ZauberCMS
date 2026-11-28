using System.Collections.Concurrent;
using System.Globalization;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;

namespace ZauberCMS.Core.Localization;

/// <summary>
/// <c>IStringLocalizer&lt;ZauberText&gt;</c>: ZauberCMS's text in the current UI culture (the admin's is
/// <c>Zauber:AdminDefaultLanguage</c>), from the <see cref="ZauberTextCatalog"/>. A missing key shows as the key
/// and is logged once; a translation whose placeholders do not match its arguments shows unformatted.
/// </summary>
internal sealed class ZauberTextLocalizer(ZauberTextCatalog catalog, ILogger<ZauberTextLocalizer> logger)
    : IStringLocalizer<ZauberText>
{
    // Keys are literals in code, so this stays small; the cap only bounds a plugin that builds keys from data
    private const int MaxReported = 1000;
    private readonly ConcurrentDictionary<string, bool> _reported = new(StringComparer.Ordinal);

    public LocalizedString this[string name]
    {
        get
        {
            ArgumentNullException.ThrowIfNull(name);
            var value = catalog.Find(name, CultureInfo.CurrentUICulture);
            if (value is null && FirstReport(name))
            {
                logger.LogWarning("The ZauberCMS text {Key} is not in any {Folder} resource file, so the key is shown",
                    name, ZauberText.ResourceFolder);
            }

            return new LocalizedString(name, value ?? name, resourceNotFound: value is null, ZauberText.ResourceFolder);
        }
    }

    public LocalizedString this[string name, params object[] arguments]
    {
        get
        {
            var text = this[name];
            try
            {
                return new LocalizedString(name, string.Format(CultureInfo.CurrentCulture, text.Value, arguments),
                    text.ResourceNotFound, text.SearchedLocation);
            }
            catch (FormatException)
            {
                // A translation with a placeholder the code does not pass ({2} for two arguments)
                if (FirstReport(name + "|format"))
                {
                    logger.LogWarning("The ZauberCMS text {Key} in {Culture} does not match its {Count} argument(s), " +
                                      "so it is shown unformatted", name, CultureInfo.CurrentUICulture.Name,
                        arguments.Length);
                }

                return text;
            }
        }
    }

    public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) =>
        catalog.All(CultureInfo.CurrentUICulture, includeParentCultures)
            .Select(entry => new LocalizedString(entry.Key, entry.Value, resourceNotFound: false, ZauberText.ResourceFolder));

    // ContainsKey takes no lock; Count takes all of them, so only a key not reported yet pays for it (ZA-302)
    private bool FirstReport(string id) =>
        !_reported.ContainsKey(id) && _reported.Count < MaxReported && _reported.TryAdd(id, true);
}
