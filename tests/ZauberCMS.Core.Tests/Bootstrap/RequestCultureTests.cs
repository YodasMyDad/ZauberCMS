using System.Globalization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Localization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ZauberCMS.Core.Installer;
using ZauberCMS.Core.Languages;
using ZauberCMS.Core.Languages.Interfaces;
using ZauberCMS.Core.Languages.Parameters;
using ZauberCMS.Core.Tests.Infrastructure;

namespace ZauberCMS.Core.Tests.Bootstrap;

/// <summary>
/// ZA-307: the account pages, the fallback pages and the sign-out page are in the request's culture (D95), which
/// <c>UseRequestLocalization</c> chooses among the site's languages. Those were read once at the start, so a
/// language added later (Settings &gt; Languages, or the installer on a site that started without a database)
/// was not a request culture until the site restarted. The request localisation of the bootstrap keeps them as
/// they are now: the real middleware, with ZauberCMS's provider first, as <c>AddZauberCms</c> sets it up.
/// </summary>
public class RequestCultureTests : IAsyncLifetime
{
    private TestSite _site = null!;

    public async ValueTask InitializeAsync()
    {
        _site = await TestSite.CreateAsync();
        // TestSite migrates itself; the bootstrap marks it when it does (InitialiseDatabaseAsync)
        _site.Services.GetRequiredService<InstallState>().MarkDatabaseInitialised();
    }

    public async ValueTask DisposeAsync() => await _site.DisposeAsync();

    [Fact]
    public async Task ZA307_a_language_added_after_the_start_is_a_request_culture_without_a_restart()
    {
        // As AddZauberCms builds them at the start: the admin's language and the languages the site had then (none)
        var options = new RequestLocalizationOptions()
            .SetDefaultCulture("en-US").AddSupportedCultures("en-US").AddSupportedUICultures("en-US");
        options.RequestCultureProviders.Insert(0, new SiteLanguagesRequestCultureProvider(options, "en-US"));

        Assert.Equal("en-US", await CultureOfRequestAsync(options, "fr-FR,fr;q=0.9"));

        // Settings > Languages adds French
        using (var scope = _site.CreateScope())
        {
            var saved = await scope.ServiceProvider.GetRequiredService<ILanguageService>().SaveLanguageAsync(
                new SaveLanguageParameters { CultureInfo = CultureInfo.GetCultureInfo("fr-FR") },
                TestContext.Current.CancellationToken);
            Assert.True(saved.Success, TestSite.Describe(saved.Messages));
        }

        Assert.Equal("fr-FR", await CultureOfRequestAsync(options, "fr-FR,fr;q=0.9"));
        // A culture the site does not have is still the default: only the site's languages are added
        Assert.Equal("en-US", await CultureOfRequestAsync(options, "de-DE"));
        Assert.DoesNotContain(options.SupportedUICultures!, c => c.Name == "de-DE");
    }

    [Fact]
    public async Task ZA307_a_site_whose_database_is_not_ready_keeps_the_start_up_cultures()
    {
        var options = new RequestLocalizationOptions()
            .SetDefaultCulture("en-US").AddSupportedCultures("en-US").AddSupportedUICultures("en-US");
        var provider = new SiteLanguagesRequestCultureProvider(options, "en-US");
        var context = new DefaultHttpContext
        {
            RequestServices = new ServiceCollection()
                .AddSingleton(new InstallState(new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build()))
                .BuildServiceProvider()
        };

        Assert.Null(await provider.DetermineProviderCultureResult(context));
        Assert.Equal(["en-US"], options.SupportedUICultures!.Select(c => c.Name));
    }

    private async Task<string> CultureOfRequestAsync(RequestLocalizationOptions options, string acceptLanguage)
    {
        using var scope = _site.CreateScope();
        var context = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
        context.Request.Headers.AcceptLanguage = acceptLanguage;
        string? culture = null;
        var middleware = new RequestLocalizationMiddleware(_ =>
        {
            culture = CultureInfo.CurrentUICulture.Name;
            return Task.CompletedTask;
        }, Options.Create(options), NullLoggerFactory.Instance);

        await middleware.Invoke(context);
        return culture!;
    }
}
