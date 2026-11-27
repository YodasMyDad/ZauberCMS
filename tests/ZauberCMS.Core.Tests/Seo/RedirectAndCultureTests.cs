using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Server.Circuits;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ZauberCMS.Core.Content.Interfaces;
using ZauberCMS.Core.Languages;
using ZauberCMS.Core.Middleware;
using ZauberCMS.Core.Seo.Interfaces;
using ZauberCMS.Core.Seo.Models;
using ZauberCMS.Core.Seo.Parameters;
using ZauberCMS.Core.Settings;
using ZauberCMS.Core.Shared.Services;
using ZauberCMS.Core.Tests.Infrastructure;

namespace ZauberCMS.Core.Tests.Seo;

/// <summary>
/// A saved redirect works on the next request (ZA-142), and the admin keeps its own language (ZA-141).
/// </summary>
public class RedirectAndCultureTests : IAsyncLifetime
{
    private TestSite _site = null!;

    public async ValueTask InitializeAsync() => _site = await TestSite.CreateAsync();

    public async ValueTask DisposeAsync() => await _site.DisposeAsync();

    [Fact]
    public async Task A_redirect_saved_after_the_list_was_read_works_on_the_next_request_and_stops_when_deleted()
    {
        var token = TestContext.Current.CancellationToken;

        // The first request makes the middleware read and keep the (empty) list.
        Assert.Null(await RedirectLocationAsync("/old-page"));

        using var scope = _site.CreateScope();
        var seoService = scope.ServiceProvider.GetRequiredService<ISeoService>();
        var saved = await seoService.SaveRedirectAsync(new SaveRedirectParameters
        {
            Redirect = new SeoRedirect { FromUrl = "/old-page", ToUrl = "https://partner.example/landing" }
        }, token);
        Assert.True(saved.Success, TestSite.Describe(saved.Messages));

        Assert.Equal("https://partner.example/landing", await RedirectLocationAsync("/old-page"));

        var deleted = await seoService.DeleteRedirectAsync(new DeleteRedirectParameters { Id = saved.Entity!.Id }, token);
        Assert.True(deleted.Success, TestSite.Describe(deleted.Messages));

        Assert.Null(await RedirectLocationAsync("/old-page"));
    }

    [Fact]
    public async Task ZA116_an_exact_redirect_with_a_question_mark_matches_only_that_address()
    {
        var token = TestContext.Current.CancellationToken;
        await SaveAsync(new SeoRedirect { FromUrl = "/search?q=old", ToUrl = "/search?q=new" }, token);

        Assert.Equal("/search?q=new", await RedirectLocationAsync("/search?q=old"));
        // As a regex "?" made the "h" optional and matched these too
        Assert.Null(await RedirectLocationAsync("/searc?q=old"));
        Assert.Null(await RedirectLocationAsync("/search?q=older"));
    }

    [Fact]
    public async Task ZA116_a_regex_redirect_matches_by_pattern_and_never_sends_an_address_to_itself()
    {
        var token = TestContext.Current.CancellationToken;
        await SaveAsync(new SeoRedirect { FromUrl = "^/blog/.*", ToUrl = "/blog/archive", IsRegex = true }, token);

        Assert.Equal("/blog/archive", await RedirectLocationAsync("/blog/2019/post"));
        Assert.Null(await RedirectLocationAsync("/blog/archive"));
    }

    [Fact]
    public async Task ZA116_a_redirect_saved_before_the_flag_keeps_the_old_regex_guess()
    {
        var token = TestContext.Current.CancellationToken;
        await SaveAsync(new SeoRedirect { FromUrl = "^/old-(.*)$", ToUrl = "/new", IsRegex = null }, token);

        Assert.Equal("/new", await RedirectLocationAsync("/old-page"));
    }

    [Theory]
    [InlineData("/loop", "/loop")]
    [InlineData("/LOOP", "/loop")]
    public async Task ZA116_a_redirect_to_itself_is_refused(string from, string to)
    {
        using var scope = _site.CreateScope();
        var result = await scope.ServiceProvider.GetRequiredService<ISeoService>().SaveRedirectAsync(
            new SaveRedirectParameters { Redirect = new SeoRedirect { FromUrl = from, ToUrl = to } },
            TestContext.Current.CancellationToken);

        Assert.False(result.Success);
    }

    [Fact]
    public async Task ZA116_a_redirect_that_closes_a_circle_is_refused()
    {
        var token = TestContext.Current.CancellationToken;
        await SaveAsync(new SeoRedirect { FromUrl = "/a", ToUrl = "/b" }, token);
        await SaveAsync(new SeoRedirect { FromUrl = "/b", ToUrl = "/c" }, token);

        using var scope = _site.CreateScope();
        var result = await scope.ServiceProvider.GetRequiredService<ISeoService>().SaveRedirectAsync(
            new SaveRedirectParameters { Redirect = new SeoRedirect { FromUrl = "/c", ToUrl = "/a" } }, token);

        Assert.False(result.Success);
        Assert.Contains(result.Messages, m => m.Message.Contains("round in circles"));
    }

    [Fact]
    public async Task ZA116_the_loop_check_follows_the_redirect_the_middleware_would_take()
    {
        var token = TestContext.Current.CancellationToken;
        // For /b the middleware takes the exact rule (exact before regex), which leads back to /a
        await SaveAsync(new SeoRedirect { FromUrl = "^/b", ToUrl = "/c", IsRegex = true }, token);
        await SaveAsync(new SeoRedirect { FromUrl = "/b", ToUrl = "/a" }, token);

        using var scope = _site.CreateScope();
        var result = await scope.ServiceProvider.GetRequiredService<ISeoService>().SaveRedirectAsync(
            new SaveRedirectParameters { Redirect = new SeoRedirect { FromUrl = "/a", ToUrl = "/b" } }, token);

        Assert.False(result.Success);
        Assert.Equal("/a", await RedirectLocationAsync("/b"));
    }

    [Theory]
    [InlineData("no-slash", false)]
    [InlineData("^/(unclosed", true)]
    public async Task ZA116_a_from_url_that_cannot_match_is_refused(string from, bool isRegex)
    {
        using var scope = _site.CreateScope();
        var result = await scope.ServiceProvider.GetRequiredService<ISeoService>().SaveRedirectAsync(
            new SaveRedirectParameters { Redirect = new SeoRedirect { FromUrl = from, ToUrl = "/x", IsRegex = isRegex } },
            TestContext.Current.CancellationToken);

        Assert.False(result.Success);
    }

    [Theory]
    [InlineData("/administration")]
    [InlineData("/accounting")]
    [InlineData("/api-docs")]
    public async Task ZA095_a_site_page_that_only_starts_like_a_system_path_is_redirected(string path)
    {
        await SaveAsync(new SeoRedirect { FromUrl = path, ToUrl = "/moved" }, TestContext.Current.CancellationToken);

        Assert.Equal("/moved", await RedirectLocationAsync(path));
    }

    [Theory]
    [InlineData("/admin", true)]
    [InlineData("/Admin/content", true)]
    [InlineData("/account/login", true)]
    [InlineData("/api/things", true)]
    [InlineData("/administration", false)]
    [InlineData("/accounting", false)]
    [InlineData("/api-docs", false)]
    [InlineData("/", false)]
    public void ZA095_system_paths_are_matched_on_whole_segments(string path, bool expected) =>
        Assert.Equal(expected, SitePaths.IsSystemPath(new PathString(path)));

    [Theory]
    [InlineData("/a-page")]
    [InlineData("/admin/content")]
    public async Task ZA098_a_page_that_throws_runs_once_and_its_exception_reaches_the_error_handler(string path)
    {
        using var scope = _site.CreateScope();
        var runs = 0;
        RequestDelegate page = _ =>
        {
            runs++;
            throw new InvalidOperationException("the page failed");
        };
        var culture = new CultureMiddleware(page, scope.ServiceProvider.GetRequiredService<IOptions<ZauberSettings>>(),
            NullLogger<CultureMiddleware>.Instance);
        var redirect = new RedirectMiddleware(ctx => culture.InvokeAsync(ctx,
            scope.ServiceProvider.GetRequiredService<IContentService>()), NullLogger<RedirectMiddleware>.Instance);

        var context = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
        context.Request.Path = path;
        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(() => redirect.InvokeAsync(context,
            scope.ServiceProvider.GetRequiredService<ISeoService>(), scope.ServiceProvider.GetRequiredService<ICacheService>()));

        Assert.Equal("the page failed", thrown.Message);
        Assert.Equal(1, runs);
    }

    [Fact]
    public async Task ZA098_the_error_page_of_a_failed_request_is_never_redirected()
    {
        await SaveAsync(new SeoRedirect { FromUrl = "^/e", ToUrl = "/elsewhere", IsRegex = true },
            TestContext.Current.CancellationToken);
        Assert.Equal("/elsewhere", await RedirectLocationAsync("/error"));

        using var scope = _site.CreateScope();
        var context = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
        context.Request.Path = "/error";
        context.Features.Set<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>(
            new Microsoft.AspNetCore.Diagnostics.ExceptionHandlerFeature
            {
                Error = new InvalidOperationException("the page failed"), Path = "/a-page"
            });
        await new RedirectMiddleware(_ => Task.CompletedTask, NullLogger<RedirectMiddleware>.Instance).InvokeAsync(context,
            scope.ServiceProvider.GetRequiredService<ISeoService>(), scope.ServiceProvider.GetRequiredService<ICacheService>());

        Assert.True(string.IsNullOrEmpty(context.Response.Headers.Location.ToString()));
    }
    private async Task SaveAsync(SeoRedirect redirect, CancellationToken token)
    {
        using var scope = _site.CreateScope();
        var saved = await scope.ServiceProvider.GetRequiredService<ISeoService>()
            .SaveRedirectAsync(new SaveRedirectParameters { Redirect = redirect }, token);
        Assert.True(saved.Success, TestSite.Describe(saved.Messages));
    }

    [Fact]
    public async Task An_admin_request_runs_in_the_admin_language_whatever_the_request_started_with()
    {
        var previous = (CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture);
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("es-ES");

            using var scope = _site.CreateScope();
            string? seenByTheRestOfTheRequest = null;
            var middleware = new CultureMiddleware(_ =>
                {
                    seenByTheRestOfTheRequest = CultureInfo.CurrentUICulture.Name;
                    return Task.CompletedTask;
                },
                scope.ServiceProvider.GetRequiredService<IOptions<ZauberSettings>>(),
                NullLogger<CultureMiddleware>.Instance);

            var context = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
            context.Request.Path = "/Admin/Content";
            await middleware.InvokeAsync(context, scope.ServiceProvider.GetRequiredService<IContentService>());

            Assert.Equal("en-US", seenByTheRestOfTheRequest);
        }
        finally
        {
            (CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture) = previous;
        }
    }

    [Theory]
    [InlineData("https://site.test/admin/content", "en-US")]
    [InlineData("https://site.test/es/pagina", "es-ES")]
    public async Task Work_on_an_admin_circuit_runs_in_the_admin_language_and_other_circuits_are_left_alone(
        string circuitAddress, string expected)
    {
        var previous = (CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture);
        try
        {
            // What the circuit took from the request that opened the connection
            CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("es-ES");

            var handler = new AdminCultureCircuitHandler(new TestNavigation(circuitAddress),
                Options.Create(new ZauberSettings { AdminDefaultLanguage = "en-US" }));
            string? seenByTheWork = null;
            var activity = handler.CreateInboundActivityHandler(_ =>
            {
                seenByTheWork = CultureInfo.CurrentUICulture.Name;
                return Task.CompletedTask;
            });

            await activity(null!);

            Assert.Equal(expected, seenByTheWork);
        }
        finally
        {
            (CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture) = previous;
        }
    }

    [Fact]
    public async Task A_circuit_that_has_no_address_yet_is_left_alone()
    {
        var handler = new AdminCultureCircuitHandler(new TestNavigation(null),
            Options.Create(new ZauberSettings { AdminDefaultLanguage = "en-US" }));
        var ran = false;

        await handler.CreateInboundActivityHandler(_ =>
        {
            ran = true;
            return Task.CompletedTask;
        })(null!);

        Assert.True(ran);
    }

    [Fact]
    public void The_site_registers_the_admin_culture_handler()
    {
        using var scope = _site.CreateScope();

        Assert.Contains(scope.ServiceProvider.GetServices<CircuitHandler>(), h => h is AdminCultureCircuitHandler);
    }

    private async Task<string?> RedirectLocationAsync(string path)
    {
        using var scope = _site.CreateScope();
        var middleware = new RedirectMiddleware(_ => Task.CompletedTask, NullLogger<RedirectMiddleware>.Instance);

        var context = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
        var query = path.IndexOf('?');
        context.Request.Path = query < 0 ? path : path[..query];
        context.Request.QueryString = query < 0 ? QueryString.Empty : new QueryString(path[query..]);
        await middleware.InvokeAsync(context, scope.ServiceProvider.GetRequiredService<ISeoService>(),
            scope.ServiceProvider.GetRequiredService<ICacheService>());

        var location = context.Response.Headers.Location.ToString();
        return string.IsNullOrEmpty(location) ? null : location;
    }

    private sealed class TestNavigation : NavigationManager
    {
        public TestNavigation(string? address)
        {
            if (address != null)
            {
                Initialize("https://site.test/", address);
            }
        }
    }
}
