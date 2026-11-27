using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using ZauberCMS.Core.Content;
using ZauberCMS.Core.Content.Interfaces;
using ZauberCMS.Core.Content.Models;
using ZauberCMS.Core.Content.Parameters;
using ZauberCMS.Core.Languages.Models;
using ZauberCMS.Core.Middleware;
using ZauberCMS.Core.Seo;
using ZauberCMS.Core.Seo.Interfaces;
using ZauberCMS.Core.Seo.Models;
using ZauberCMS.Core.Seo.Parameters;
using ZauberCMS.Core.Shared.Services;
using ZauberCMS.Core.Tests.Infrastructure;

namespace ZauberCMS.Core.Tests.Seo;

/// <summary>
/// ZA-165 (Roadmap D42): a redirect with a domain applies only on that domain; one without applies on every host,
/// and for the same address the one for the request's domain wins. ZA-166: a domain's path matches whole segments,
/// and the longest matching domain is the request's.
/// </summary>
public class RedirectDomainTests : IAsyncLifetime
{
    private TestSite _site = null!;
    private ContentType _pageType = null!;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        _site = await TestSite.CreateAsync();
        _pageType = await _site.CreateContentTypeAsync("page");
    }

    public async ValueTask DisposeAsync() => await _site.DisposeAsync();

    [Theory]
    [InlineData("example.test", "example.test", "/", true)]
    [InlineData("example.test", "EXAMPLE.test", "/any/page", true)]
    [InlineData("example.test", "other.test", "/", false)]
    [InlineData("example.test/fr", "example.test", "/fr", true)]
    [InlineData("example.test/fr", "example.test", "/fr/page", true)]
    [InlineData("example.test/fr/", "example.test", "/FR/page", true)]
    [InlineData("example.test/fr", "example.test", "/french", false)]
    [InlineData("example.test/fr", "example.test", "/", false)]
    [InlineData("example.test/", "example.test", "/page", true)]
    [InlineData("", "example.test", "/", false)]
    public void ZA166_a_domain_matches_its_host_and_its_path_on_whole_segments(string domainUrl, string host,
        string path, bool expected) =>
        Assert.Equal(expected, DomainMatching.Matches(domainUrl, host, path));

    [Fact]
    public void ZA166_the_longest_matching_domain_is_the_requests_whatever_the_order()
    {
        var whole = new Domain { Url = "example.test" };
        var french = new Domain { Url = "example.test/fr" };

        Assert.Same(french, DomainMatching.FindDomain([whole, french], "example.test", "/fr/page"));
        Assert.Same(french, DomainMatching.FindDomain([french, whole], "example.test", "/fr/page"));
        Assert.Same(whole, DomainMatching.FindDomain([french, whole], "example.test", "/french"));
        Assert.Null(DomainMatching.FindDomain([french, whole], "other.test", "/fr"));
    }

    [Fact]
    public async Task ZA165_a_redirect_for_a_domain_applies_only_on_that_domain()
    {
        var domain = await AddDomainAsync("example.test");
        await SaveAsync(new SeoRedirect { FromUrl = "/old", ToUrl = "/new", DomainId = domain.Id });

        Assert.Equal("/new", await RedirectLocationAsync("example.test", "/old"));
        Assert.Null(await RedirectLocationAsync("other.test", "/old"));
    }

    [Fact]
    public async Task ZA165_a_redirect_without_a_domain_applies_on_every_host()
    {
        await AddDomainAsync("example.test");
        await SaveAsync(new SeoRedirect { FromUrl = "/old", ToUrl = "/new" });

        Assert.Equal("/new", await RedirectLocationAsync("example.test", "/old"));
        Assert.Equal("/new", await RedirectLocationAsync("other.test", "/old"));
    }

    [Fact]
    public async Task ZA165_for_the_same_address_the_redirect_for_the_requests_domain_wins()
    {
        var domain = await AddDomainAsync("example.test");
        await SaveAsync(new SeoRedirect { FromUrl = "/old", ToUrl = "/for-example", DomainId = domain.Id });
        // Saved later, so newer: it would win if the domain did not come first
        await SaveAsync(new SeoRedirect { FromUrl = "/old", ToUrl = "/for-everyone" });

        Assert.Equal("/for-example", await RedirectLocationAsync("example.test", "/old"));
        Assert.Equal("/for-everyone", await RedirectLocationAsync("other.test", "/old"));
    }

    [Fact]
    public async Task ZA165_a_regex_redirect_for_a_domain_applies_only_on_that_domain_and_comes_first()
    {
        var domain = await AddDomainAsync("example.test");
        await SaveAsync(new SeoRedirect { FromUrl = "^/blog/.*", ToUrl = "/news", IsRegex = true, DomainId = domain.Id });
        await SaveAsync(new SeoRedirect { FromUrl = "^/blog/.*", ToUrl = "/articles", IsRegex = true });

        Assert.Equal("/news", await RedirectLocationAsync("example.test", "/blog/a-post"));
        Assert.Equal("/articles", await RedirectLocationAsync("other.test", "/blog/a-post"));
    }

    [Fact]
    public async Task ZA165_a_domain_with_a_path_covers_only_the_addresses_below_it()
    {
        var french = await AddDomainAsync("example.test/fr");
        await SaveAsync(new SeoRedirect { FromUrl = "/fr/ancien", ToUrl = "/fr/nouveau", DomainId = french.Id });
        await SaveAsync(new SeoRedirect { FromUrl = "/frontpage", ToUrl = "/", DomainId = french.Id });

        Assert.Equal("/fr/nouveau", await RedirectLocationAsync("example.test", "/fr/ancien"));
        Assert.Null(await RedirectLocationAsync("example.test", "/frontpage"));
    }

    [Fact]
    public async Task ZA165_a_domain_whose_address_is_changed_is_matched_by_its_new_address()
    {
        var domain = await AddDomainAsync("example.test");
        await SaveAsync(new SeoRedirect { FromUrl = "/old", ToUrl = "/new", DomainId = domain.Id });
        Assert.Equal("/new", await RedirectLocationAsync("example.test", "/old"));

        domain.Url = "renamed.test";
        await SaveDomainAsync(domain);

        Assert.Null(await RedirectLocationAsync("example.test", "/old"));
        Assert.Equal("/new", await RedirectLocationAsync("renamed.test", "/old"));
    }

    [Fact]
    public async Task ZA165_redirects_on_two_domains_do_not_make_a_loop_but_one_for_every_domain_does()
    {
        var first = await AddDomainAsync("first.test");
        var second = await AddDomainAsync("second.test");
        await SaveAsync(new SeoRedirect { FromUrl = "/a", ToUrl = "/b", DomainId = first.Id });

        // On second.test /a is not redirected, so /b -> /a there goes nowhere
        await SaveAsync(new SeoRedirect { FromUrl = "/b", ToUrl = "/a", DomainId = second.Id });

        // On first.test this one and the first redirect would send visitors round in circles
        using var scope = _site.CreateScope();
        var refused = await scope.ServiceProvider.GetRequiredService<ISeoService>().SaveRedirectAsync(
            new SaveRedirectParameters { Redirect = new SeoRedirect { FromUrl = "/b", ToUrl = "/a" } }, Ct);
        Assert.False(refused.Success);
    }

    private async Task<Domain> AddDomainAsync(string url)
    {
        var root = await _site.CreateContentAsync(_pageType, $"Root {url}");
        var languageId = await _site.QueryDatabaseAsync(async db =>
        {
            var language = await db.Languages.FirstOrDefaultAsync(l => l.LanguageIsoCode == "en-GB", Ct);
            if (language == null)
            {
                language = new Language { LanguageIsoCode = "en-GB", LanguageCultureName = "English" };
                db.Languages.Add(language);
                await db.SaveChangesAsync(Ct);
            }

            return language.Id;
        });

        var domain = new Domain { Url = url, ContentId = root.Id, LanguageId = languageId };
        await SaveDomainAsync(domain);
        return domain;
    }

    private async Task SaveDomainAsync(Domain domain)
    {
        using var scope = _site.CreateScope();
        var saved = await scope.ServiceProvider.GetRequiredService<IContentService>()
            .SaveDomainAsync(new SaveDomainParameters { Domain = domain }, Ct);
        Assert.True(saved.Success, TestSite.Describe(saved.Messages));
    }

    private async Task SaveAsync(SeoRedirect redirect)
    {
        using var scope = _site.CreateScope();
        var saved = await scope.ServiceProvider.GetRequiredService<ISeoService>()
            .SaveRedirectAsync(new SaveRedirectParameters { Redirect = redirect }, Ct);
        Assert.True(saved.Success, TestSite.Describe(saved.Messages));
    }

    private async Task<string?> RedirectLocationAsync(string host, string path)
    {
        using var scope = _site.CreateScope();
        var middleware = new RedirectMiddleware(_ => Task.CompletedTask, NullLogger<RedirectMiddleware>.Instance);

        var context = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
        context.Request.Host = new HostString(host);
        context.Request.Path = path;
        await middleware.InvokeAsync(context, scope.ServiceProvider.GetRequiredService<ISeoService>(),
            scope.ServiceProvider.GetRequiredService<ICacheService>());

        var location = context.Response.Headers.Location.ToString();
        return string.IsNullOrEmpty(location) ? null : location;
    }
}
