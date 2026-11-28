using System.Globalization;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using ZauberCMS.Components.Editors;
using ZauberCMS.Core.Localization;
using ZauberCMS.Core.Tests.Infrastructure;

namespace ZauberCMS.Core.Tests.Localization;

/// <summary>
/// Roadmap Phase 10h-1 (D37, D90, ZA-129): ZauberCMS's text comes from the <c>ZauberText</c> resource files of the
/// scanned assemblies. A site's or plugin's translation wins over ZauberCMS's English, a more specific culture
/// wins over a less specific one, and a key nobody translated falls back to English. This test assembly plays the
/// site: its <c>Localization/ZauberText</c> folder holds French files (and a plugin area, <c>TestPlugin</c>).
/// </summary>
public class ZauberTextTests
{
    private static readonly Assembly Core = typeof(ZauberText).Assembly;
    private static readonly Assembly Components = typeof(TextProperty).Assembly;
    private static readonly Assembly Site = typeof(ZauberTextTests).Assembly;

    private static readonly CultureInfo French = CultureInfo.GetCultureInfo("fr");

    private static ZauberTextCatalog Catalog(params Assembly[] siteAndPlugins) =>
        ZauberTextCatalog.FromAssemblies([Core, Components, .. siteAndPlugins], [Core, Components],
            NullLoggerFactory.Instance);

    [Fact]
    public void ZA129_ZauberCMS_ships_its_English_text_in_its_assemblies()
    {
        var catalog = Catalog();

        Assert.Equal("Save", catalog.Find("general_save", CultureInfo.InvariantCulture));
        Assert.Equal("Content", catalog.Find("sections_content", CultureInfo.GetCultureInfo("en-US")));
        // No translation is shipped: every language gets the English
        Assert.Equal("Save", catalog.Find("general_save", French));
        Assert.Null(catalog.Find("general_noSuchKey", French));
    }

    [Fact]
    public void D90_a_site_translation_wins_and_a_key_it_does_not_translate_falls_back_to_English()
    {
        var catalog = Catalog(Site);

        Assert.Equal("Enregistrer", catalog.Find("general_save", French));
        // A culture with no file of its own uses its parent's
        Assert.Equal("Enregistrer", catalog.Find("general_save", CultureInfo.GetCultureInfo("fr-FR")));
        Assert.Equal("Enregistrer", catalog.Find("general_save", CultureInfo.GetCultureInfo("fr-CA")));
        // The more specific culture first
        Assert.Equal("Annuler (Canada)", catalog.Find("general_cancel", CultureInfo.GetCultureInfo("fr-CA")));
        // Not translated: ZauberCMS's English
        Assert.Equal("Cancel", catalog.Find("general_cancel", French));
        // Other languages are untouched
        Assert.Equal("Save", catalog.Find("general_save", CultureInfo.GetCultureInfo("en-GB")));
        Assert.Equal("Save", catalog.Find("general_save", CultureInfo.GetCultureInfo("de")));
    }

    [Fact]
    public void D90_a_plugin_brings_its_own_area_in_English_and_its_translation()
    {
        var catalog = Catalog(Site);

        Assert.Equal("Plugin title", catalog.Find("testPlugin_title", CultureInfo.GetCultureInfo("en")));
        Assert.Equal("Titre du plugin", catalog.Find("testPlugin_title", French));
        Assert.Equal("Only in English", catalog.Find("testPlugin_onlyEnglish", French));
    }

    [Fact]
    public void ZA300_a_file_named_for_a_culture_NET_does_not_know_is_not_read_as_a_file_with_no_culture()
    {
        // General.xx.resx: the SDK builds it into this assembly itself (no satellite), as it does General.dk.resx
        var resources = Site.GetManifestResourceNames();
        Assert.Contains(resources, n => n.EndsWith(".ZauberText.General.xx.resources", StringComparison.Ordinal));

        var log = new RecordingLog();
        var catalog = ZauberTextCatalog.FromAssemblies([Core, Components, Site], [Core, Components], log);

        // Before ZA-300 its "xx Save" replaced the English in every language without a translation of its own
        Assert.Equal("Save", catalog.Find("general_save", CultureInfo.GetCultureInfo("en")));
        Assert.Equal("Save", catalog.Find("general_save", CultureInfo.GetCultureInfo("de")));
        Assert.Equal("Enregistrer", catalog.Find("general_save", French));
        Assert.Single(log.Warnings, w => w.Contains("General.xx.resources", StringComparison.Ordinal) &&
                                         w.Contains("xx", StringComparison.Ordinal));
        // A second look reads the table it kept: still one warning
        Assert.Equal("Save", catalog.Find("general_save", CultureInfo.InvariantCulture));
        Assert.Single(log.Warnings);
    }

    [Fact]
    public void ZA302_a_missing_key_and_a_translation_that_does_not_format_are_each_logged_once()
    {
        var log = new RecordingLog();
        var localizer = new ZauberTextLocalizer(
            new ZauberTextCatalog([new FakeLayer().With("", "test_bad", "Only {1}")]),
            log.CreateLogger<ZauberTextLocalizer>());

        for (var i = 0; i < 3; i++)
        {
            Assert.Equal("test_missing", localizer["test_missing"].Value);
            Assert.Equal("Only {1}", localizer["test_bad", "x"].Value);
        }

        Assert.Equal(2, log.Warnings.Count);
        Assert.Single(log.Warnings, w => w.Contains("test_missing", StringComparison.Ordinal));
        Assert.Single(log.Warnings, w => w.Contains("test_bad", StringComparison.Ordinal));
    }

    [Fact]
    public void D90_a_more_specific_culture_wins_first_then_the_site_over_plugins_over_ZauberCMS()
    {
        var site = new FakeLayer().With("", "key_a", "site neutral").With("", "key_c", "site c");
        var firstPlugin = new FakeLayer().With("", "key_c", "first plugin c").With("", "key_d", "first plugin d");
        var secondPlugin = new FakeLayer().With("", "key_d", "second plugin d");
        var zauber = new FakeLayer().With("", "key_a", "English").With("fr", "key_a", "French")
            .With("", "key_b", "English b");
        var catalog = new ZauberTextCatalog([site, firstPlugin, secondPlugin, zauber]);

        // ZauberCMS's French beats the site's file with no culture: the culture is looked at first
        Assert.Equal("French", catalog.Find("key_a", French));
        // At the same step the site wins: its neutral file changes ZauberCMS's English
        Assert.Equal("site neutral", catalog.Find("key_a", CultureInfo.GetCultureInfo("en")));
        Assert.Equal("English b", catalog.Find("key_b", French));
        Assert.Equal("site c", catalog.Find("key_c", French));
        Assert.Equal("first plugin d", catalog.Find("key_d", French));

        var all = catalog.All(French, includeParentCultures: true);
        Assert.Equal("French", all["key_a"]);
        Assert.Equal(4, all.Count);
        Assert.Equal(["key_a"], catalog.All(French, includeParentCultures: false).Keys);
    }

    [Fact]
    public void D90_the_localiser_shows_a_missing_key_as_the_key_and_formats_in_the_current_culture()
    {
        var localizer = new ZauberTextLocalizer(new ZauberTextCatalog([
                new FakeLayer().With("", "test_count", "{0:N0} items").With("", "test_bad", "Only {1}")
            ]),
            NullLogger<ZauberTextLocalizer>.Instance);
        var previous = (CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture);
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("de");

            var missing = localizer["test_missing"];
            Assert.True(missing.ResourceNotFound);
            Assert.Equal("test_missing", missing.Value);

            Assert.Equal("1.234 items", localizer["test_count", 1234].Value);
            // A translation with a placeholder the code does not pass shows unformatted instead of throwing
            Assert.Equal("Only {1}", localizer["test_bad", "x"].Value);
            Assert.Contains(localizer.GetAllStrings(true), s => s.Name == "test_count");
        }
        finally
        {
            (CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture) = previous;
        }
    }

    [Fact]
    public async Task D90_a_site_gets_its_text_and_Radzen_s_through_the_bootstrap()
    {
        var contentRoot = TestSite.CreateContentRoot();
        var previous = CultureInfo.CurrentUICulture;
        try
        {
            // The site passes this assembly as a plugin: its ZauberText files are a layer over ZauberCMS's
            await using var app = TestSite.BuildApp("", "", contentRoot, validateContainer: true,
                additionalAssemblies: [Site]);
            var text = app.Services.GetRequiredService<IStringLocalizer<ZauberText>>();
            var radzen = app.Services.GetRequiredService<Radzen.ILocalizer>();

            CultureInfo.CurrentUICulture = French;
            Assert.Equal("Enregistrer", text["general_save"].Value);
            Assert.Equal("Cancel", text["general_cancel"].Value);

            // Radzen's key DataGrid_EmptyText is radzen_DataGrid_EmptyText; without one, Radzen's own English (null)
            Assert.Equal("Aucun enregistrement.", radzen.Get("DataGrid_EmptyText", French));
            Assert.Null(radzen.Get("DataGrid_EqualsText", French));
            Assert.Null(radzen.Get("DataGrid_EmptyText", CultureInfo.GetCultureInfo("en")));
        }
        finally
        {
            CultureInfo.CurrentUICulture = previous;
            Directory.Delete(contentRoot, recursive: true);
        }
    }

    /// <summary>The warnings logged through it, as text (a logger factory that is its own logger).</summary>
    private sealed class RecordingLog : ILoggerFactory, ILogger
    {
        public List<string> Warnings { get; } = [];

        public ILogger CreateLogger(string categoryName) => this;
        public void AddProvider(ILoggerProvider provider) { }
        public void Dispose() { }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (logLevel == LogLevel.Warning)
            {
                lock (Warnings)
                {
                    Warnings.Add(formatter(state, exception));
                }
            }
        }
    }

    private sealed class FakeLayer : IZauberTextLayer
    {
        private readonly Dictionary<string, Dictionary<string, string>> _cultures = new(StringComparer.Ordinal);

        public FakeLayer With(string culture, string key, string value)
        {
            if (!_cultures.TryGetValue(culture, out var table))
            {
                _cultures[culture] = table = new Dictionary<string, string>(StringComparer.Ordinal);
            }

            table[key] = value;
            return this;
        }

        public IReadOnlyDictionary<string, string> Load(CultureInfo culture) =>
            _cultures.TryGetValue(culture.Name, out var table) ? table : new Dictionary<string, string>();
    }
}
