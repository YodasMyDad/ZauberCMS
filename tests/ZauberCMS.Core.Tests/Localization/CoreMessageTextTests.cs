using System.Globalization;
using System.Reflection;
using HtmlAgilityPack;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using ZauberCMS.Components.Editors;
using ZauberCMS.Core.Localization;
using ZauberCMS.Core.Plugins;
using ZauberCMS.Core.Providers;
using ZauberCMS.Core.Seo.Checks;
using ZauberCMS.Core.Seo.Models;
using ZauberCMS.Core.Tests.Infrastructure;
using ContentItemModel = ZauberCMS.Core.Content.Models.Content;

namespace ZauberCMS.Core.Tests.Localization;

/// <summary>
/// Roadmap Phase 10h-2 (D37, D92, ZA-129): Core writes the text it builds for a person (content, media and storage
/// messages, the SEO checks) in the caller's culture, from Core's own files, so a site's translation of a Core key
/// is what the admin shows; under the default culture the text is ZauberCMS's English, as before.
/// </summary>
public class CoreMessageTextTests
{
    private static readonly Assembly Core = typeof(ZauberText).Assembly;
    private static readonly Assembly Components = typeof(TextProperty).Assembly;
    private static readonly Assembly Site = typeof(CoreMessageTextTests).Assembly;

    [Fact]
    public async Task D92_an_SEO_check_discovery_builds_writes_its_name_and_result_in_the_caller_s_culture()
    {
        var contentRoot = TestSite.CreateContentRoot();
        var previous = CultureInfo.CurrentUICulture;
        try
        {
            // The site passes this assembly as a plugin: its ZauberText/Seo.fr.resx translates two Core keys
            await using var app = TestSite.BuildApp("", "", contentRoot, validateContainer: true,
                additionalAssemblies: [Site]);
            // Built as the SEO checker builds it: discovery, the localiser by constructor (D97), the instance cached
            using var scope = app.Services.CreateScope();
            ISeoCheck check = scope.ServiceProvider.GetRequiredService<ExtensionManager>().GetInstances<ISeoCheck>(true)
                .Values.OfType<PageTitleSeoCheck>().Single();
            var withoutTitle = new HtmlDocument();
            withoutTitle.LoadHtml("<html><head></head><body></body></html>");

            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en");
            var english = await check.Check("https://example.com/", withoutTitle, new ContentItemModel(),
                TestContext.Current.CancellationToken);
            Assert.Equal("Page Title Checker", check.Name);
            Assert.Equal("Page title is missing.", Assert.Single(english.Items).DefaultMessage);

            // The same (cached) instance answers in the culture of each call
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("fr");
            var french = await check.Check("https://example.com/", withoutTitle, new ContentItemModel(),
                TestContext.Current.CancellationToken);
            Assert.Equal("Titre de la page", check.Name);
            Assert.Equal("Titre de la page", french.Name);
            Assert.Equal("Le titre de la page manque.", Assert.Single(french.Items).DefaultMessage);
        }
        finally
        {
            CultureInfo.CurrentUICulture = previous;
            Directory.Delete(contentRoot, recursive: true);
        }
    }

    [Fact]
    public void Phase11h_the_no_storage_provider_refusal_s_English_is_the_public_constant()
    {
        var catalog = ZauberTextCatalog.FromAssemblies([Core, Components], [Core, Components], NullLoggerFactory.Instance);

        // MediaService adds media_noStorageProvider; code that compared with the constant keeps working in English
        Assert.Equal(ProviderService.NoStorageProviderMessage,
            catalog.Find("media_noStorageProvider", CultureInfo.InvariantCulture));
    }
}
