using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Server.Circuits;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ZauberCMS.Core.Content.Interfaces;
using ZauberCMS.Core.Content.Models;
using ZauberCMS.Core.Languages;
using ZauberCMS.Core.Languages.Models;
using ZauberCMS.Core.Middleware;
using ZauberCMS.Core.Settings;
using ZauberCMS.Core.Tests.Infrastructure;

namespace ZauberCMS.Core.Tests.Seo;

/// <summary>
/// ZA-143: only the content catch-all page is looked up as content, and a front-end circuit runs in the language
/// of the page it is on (not the root page's).
/// </summary>
public class ContentCultureTests : IAsyncLifetime
{
    private TestSite _site = null!;

    public async ValueTask InitializeAsync()
    {
        _site = await TestSite.CreateAsync();
        var pageType = await _site.CreateContentTypeAsync("page");
        var home = await _site.CreateContentAsync(pageType, "Home");
        var about = await _site.CreateContentAsync(pageType, "About", home.Id);
        await _site.QueryDatabaseAsync(async db =>
        {
            var german = new Language { LanguageIsoCode = "de-DE", LanguageCultureName = "German" };
            var french = new Language { LanguageIsoCode = "fr-FR", LanguageCultureName = "French" };
            db.Languages.AddRange(german, french);
            (await db.Contents.SingleAsync(c => c.Id == home.Id, Ct)).LanguageId = german.Id;
            (await db.Contents.SingleAsync(c => c.Id == about.Id, Ct)).LanguageId = french.Id;
            return await db.SaveChangesAsync(Ct);
        });
    }

    public async ValueTask DisposeAsync() => await _site.DisposeAsync();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("/{**slug}", true)]
    [InlineData("{**slug}", true)]
    [InlineData("/blog/{**slug}", false)]
    [InlineData("/account/login", false)]
    [InlineData("/{**path}", false)]
    public void Only_the_catch_all_slug_route_is_content(string route, bool expected)
    {
        Assert.Equal(expected, CultureMiddleware.IsContentEndpoint(Endpoint(route)));
        Assert.False(CultureMiddleware.IsContentEndpoint(null));
    }

    // The installer's "language of the site" (GlobalSettings.DefaultLanguage) is the language of pages that
    // have none of their own, before the admin's language
    [Fact]
    public async Task A_page_without_a_language_runs_in_the_default_language_of_the_site()
    {
        var pageType = await _site.CreateContentTypeAsync("plain");
        var orphan = await _site.CreateContentAsync(pageType, "Plain");
        // Not under the German home page: nothing in its path has a language
        await _site.QueryDatabaseAsync(async db =>
        {
            var page = await db.Contents.SingleAsync(c => c.Id == orphan.Id, Ct);
            page.IsRootContent = false;
            return await db.SaveChangesAsync(Ct);
        });

        var before = await RunMiddlewareAsync("/plain", Endpoint("/{**slug}"));
        Assert.Equal("en-US", before.Culture);

        using (var scope = _site.CreateScope())
        {
            var dataService = scope.ServiceProvider.GetRequiredService<ZauberCMS.Core.Data.Interfaces.IDataService>();
            var settings = await ZauberCMS.Core.Extensions.ServiceExtensions.GetGlobalSettings(dataService);
            settings.DefaultLanguage = "it-IT";
            Assert.True(await ZauberCMS.Core.Extensions.ServiceExtensions.SaveGlobalSettings(dataService, settings));
            scope.ServiceProvider.GetRequiredService<ZauberCMS.Core.Shared.Services.ICacheService>()
                .ClearCachedItemsWithPrefix(nameof(Content));
        }

        var after = await RunMiddlewareAsync("/plain", Endpoint("/{**slug}"));
        Assert.NotNull(after.Entry?.Content);
        Assert.Equal("it-IT", after.Culture);
    }

    // ZA-148: "/" is answered by a page of its own (Index.razor), not by the catch-all
    [Fact]
    public void The_home_page_route_is_content_only_when_a_Razor_page_answers_it()
    {
        Assert.True(CultureMiddleware.IsContentEndpoint(HomePageEndpoint()));
        Assert.False(CultureMiddleware.IsContentEndpoint(Endpoint("/")));
    }

    [Fact]
    public async Task The_home_page_runs_in_the_language_of_the_root_page()
    {
        var (entry, culture) = await RunMiddlewareAsync("/", HomePageEndpoint());

        Assert.NotNull(entry?.Content);
        Assert.Equal("de-DE", culture);
    }

    [Fact]
    public async Task A_request_that_is_not_a_page_is_not_looked_up_as_the_root_page()
    {
        var (entry, culture) = await RunMiddlewareAsync("/_blazor/negotiate", Endpoint("/_blazor/negotiate"));

        Assert.Null(entry);
        Assert.Equal("es-ES", culture);
    }

    [Theory]
    [InlineData("/", "de-DE")]
    [InlineData("/about", "fr-FR")]
    public async Task A_page_request_runs_in_the_language_of_its_page(string path, string expected)
    {
        var (entry, culture) = await RunMiddlewareAsync(path, Endpoint("/{**slug}"));

        Assert.NotNull(entry?.Content);
        Assert.Equal(expected, culture);
    }

    [Theory]
    [InlineData("https://site.test/", "de-DE")]
    [InlineData("https://site.test/about", "fr-FR")]
    [InlineData("https://site.test/About/", "fr-FR")]
    [InlineData("https://site.test/about?tab=2#team", "fr-FR")]
    [InlineData("https://site.test/no-such-page", "es-ES")]
    [InlineData("https://site.test/admin/content", "es-ES")] // AdminCultureCircuitHandler's job
    public async Task A_front_end_circuit_runs_in_the_language_of_its_page(string address, string expected)
    {
        var previous = (CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture);
        try
        {
            // What the circuit took from its /_blazor request
            CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("es-ES");
            using var scope = _site.CreateScope();
            var handler = new ContentCultureCircuitHandler(new TestNavigation(address),
                scope.ServiceProvider.GetRequiredService<IContentService>(),
                scope.ServiceProvider.GetRequiredService<IOptions<ZauberSettings>>(),
                NullLogger<ContentCultureCircuitHandler>.Instance);
            var seen = new List<string>();
            var activity = handler.CreateInboundActivityHandler(_ =>
            {
                seen.Add(CultureInfo.CurrentUICulture.Name);
                return Task.CompletedTask;
            });

            await activity(null!);
            await activity(null!); // the second one uses what the first one found

            Assert.Equal([expected, expected], seen);
        }
        finally
        {
            (CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture) = previous;
        }
    }

    [Fact]
    public async Task A_site_under_a_path_base_finds_the_page_of_its_circuit()
    {
        var previous = (CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture);
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("es-ES");
            using var scope = _site.CreateScope();
            var handler = new ContentCultureCircuitHandler(
                new TestNavigation("https://site.test/site/about", "https://site.test/site/"),
                scope.ServiceProvider.GetRequiredService<IContentService>(),
                scope.ServiceProvider.GetRequiredService<IOptions<ZauberSettings>>(),
                NullLogger<ContentCultureCircuitHandler>.Instance);
            string? seen = null;

            await handler.CreateInboundActivityHandler(_ =>
            {
                seen = CultureInfo.CurrentUICulture.Name;
                return Task.CompletedTask;
            })(null!);

            Assert.Equal("fr-FR", seen);
        }
        finally
        {
            (CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture) = previous;
        }
    }

    [Fact]
    public async Task ZA147_A_circuit_that_moves_to_another_page_runs_the_next_activity_in_that_pages_language()
    {
        var previous = (CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture);
        try
        {
            // Enhanced navigation: the address changes in one activity; the new page's components start in a
            // later one (seen in the browser with the sample site: German -> Spanish page)
            CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("es-ES");
            using var scope = _site.CreateScope();
            var navigation = new TestNavigation("https://site.test/about");
            var handler = new ContentCultureCircuitHandler(navigation,
                scope.ServiceProvider.GetRequiredService<IContentService>(),
                scope.ServiceProvider.GetRequiredService<IOptions<ZauberSettings>>(),
                NullLogger<ContentCultureCircuitHandler>.Instance);
            var seen = new List<string>();
            var activity = handler.CreateInboundActivityHandler(_ =>
            {
                seen.Add(CultureInfo.CurrentUICulture.Name);
                return Task.CompletedTask;
            });

            await activity(null!);
            navigation.MoveTo("https://site.test/");
            await activity(null!);

            Assert.Equal(["fr-FR", "de-DE"], seen);
        }
        finally
        {
            (CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture) = previous;
        }
    }

    [Fact]
    public void The_site_registers_the_content_culture_handler()
    {
        using var scope = _site.CreateScope();

        Assert.Contains(scope.ServiceProvider.GetServices<CircuitHandler>(), h => h is ContentCultureCircuitHandler);
    }

    private async Task<(EntryModel? Entry, string Culture)> RunMiddlewareAsync(string path, Endpoint endpoint)
    {
        var previous = (CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture);
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("es-ES");
            using var scope = _site.CreateScope();
            string? culture = null;
            var middleware = new CultureMiddleware(_ =>
                {
                    culture = CultureInfo.CurrentUICulture.Name;
                    return Task.CompletedTask;
                },
                scope.ServiceProvider.GetRequiredService<IOptions<ZauberSettings>>(),
                NullLogger<CultureMiddleware>.Instance);

            var context = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
            context.Request.Scheme = "https";
            context.Request.Host = new HostString("site.test");
            context.Request.Path = path;
            context.SetEndpoint(endpoint);
            if (endpoint is RouteEndpoint { RoutePattern.Parameters.Count: > 0 } && path.Trim('/').Length > 0)
            {
                context.Request.RouteValues["slug"] = path.Trim('/');
            }

            await middleware.InvokeAsync(context, scope.ServiceProvider.GetRequiredService<IContentService>());
            return (CultureMiddleware.GetEntryModel(context), culture!);
        }
        finally
        {
            (CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture) = previous;
        }
    }

    private static RouteEndpoint Endpoint(string route) =>
        new(_ => Task.CompletedTask, RoutePatternFactory.Parse(route), 0, EndpointMetadataCollection.Empty, route);

    // What MapRazorComponents builds for a page with @page "/"
    private static RouteEndpoint HomePageEndpoint() =>
        new(_ => Task.CompletedTask, RoutePatternFactory.Parse("/"), 0,
            new EndpointMetadataCollection(
                new Microsoft.AspNetCore.Components.Endpoints.ComponentTypeMetadata(typeof(HomePage))), "/");

    private sealed class HomePage : ComponentBase;

    private sealed class TestNavigation : NavigationManager
    {
        public TestNavigation(string address, string baseAddress = "https://site.test/") =>
            Initialize(baseAddress, address);

        public void MoveTo(string address) => Uri = address;
    }
}
