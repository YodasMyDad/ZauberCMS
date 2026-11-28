using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ZauberCMS.Core.Membership;
using ZauberCMS.Core.Middleware;
using ZauberCMS.Core.Tests.Infrastructure;

namespace ZauberCMS.Core.Tests.Security;

/// <summary>
/// ZA-252 (Roadmap D143): a site under a path base reads its own cookies when the browser also holds cookies of the
/// same names at <c>/</c> (another site on the host, or this one before it moved). ASP.NET Core writes the site's
/// cookies at its path base, the browser sends the longer path first, and ASP.NET Core reads the last cookie of a
/// name: the stale one. The sign-in did not hold and a form post was refused until the cookies were cleared.
/// </summary>
public class PathBaseCookieTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    // What a browser holds from a site at the host's root that had other keys: not readable here
    private const string StaleValue = "CfDJ8Stale-cookie-of-another-site";

    [Theory]
    [InlineData("/site", "site")]
    // At the host's root the framework's choice is kept (the site's cookies are at "/" there)
    [InlineData("", "stale")]
    public async Task ZA252_of_two_cookies_with_one_name_the_first_is_read_under_a_path_base(string pathBase,
        string expected)
    {
        var context = new DefaultHttpContext();
        context.Request.PathBase = pathBase;
        // As a browser sends them: the cookie of the longer path (/site) first
        context.Request.Headers.Cookie = "a=site; b=one; a=stale; B=two; c=\"quoted\"";
        string? read = null;
        IRequestCookieCollection? cookies = null;

        await new PathBaseCookiesMiddleware(ctx =>
        {
            read = ctx.Request.Cookies["a"];
            cookies = ctx.Request.Cookies;
            return Task.CompletedTask;
        }).InvokeAsync(context);

        Assert.Equal(expected, read);
        if (pathBase.Length > 0)
        {
            // Names compare ignoring case, as the framework's collection does; the rest is read as the framework reads it
            Assert.Equal("one", cookies!["b"]);
            Assert.Equal(3, cookies.Count);
            Assert.Equal(new DefaultHttpContext { Request = { Headers = { Cookie = "c=\"quoted\"" } } }.Request.Cookies["c"],
                cookies["c"]);
            // The header is left as the browser sent it
            Assert.Equal("a=site; b=one; a=stale; B=two; c=\"quoted\"", context.Request.Headers.Cookie.ToString());
        }
    }

    [Fact]
    public async Task ZA252_without_a_repeated_name_the_cookies_are_the_frameworks()
    {
        var context = new DefaultHttpContext();
        context.Request.PathBase = "/site";
        context.Request.Headers.Cookie = "a=1%202; b=x";
        var framework = new DefaultHttpContext { Request = { Headers = { Cookie = "a=1%202; b=x" } } }.Request.Cookies;
        IRequestCookieCollection? cookies = null;

        await new PathBaseCookiesMiddleware(ctx =>
        {
            cookies = ctx.Request.Cookies;
            // A header changed later is read again, as the framework reads it
            ctx.Request.Headers.Cookie = "a=later; a=stale";
            Assert.Equal("later", ctx.Request.Cookies["a"]);
            return Task.CompletedTask;
        }).InvokeAsync(context);

        Assert.Equal(framework.OrderBy(c => c.Key), cookies!.OrderBy(c => c.Key));
    }

    [Fact]
    public async Task ZA252_under_a_path_base_the_sign_in_holds_and_a_form_posts_with_stale_cookies_at_the_root()
    {
        var contentRoot = TestSite.CreateContentRoot();
        await using var app = TestSite.BuildApp("Sqlite", "DataSource=:memory:", contentRoot, validateContainer: false,
            new Dictionary<string, string?> { ["urls"] = "http://127.0.0.1:0" });

        // The start of the pipeline AddZauberCms builds, under the site's path base
        app.UsePathBase("/site");
        app.UseMiddleware<PathBaseCookiesMiddleware>();
        app.UseRouting();
        app.UseAuthentication();
        app.MapPost("/signin", async (HttpContext context) =>
        {
            await context.SignInAsync(IdentityConstants.ApplicationScheme,
                new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Name, "ada")], "Test")));
            return Results.Ok();
        });
        app.MapGet("/who", (HttpContext context) => context.User.Identity?.Name ?? "nobody");
        app.MapGet("/form", (HttpContext context, IAntiforgery antiforgery) =>
            antiforgery.GetAndStoreTokens(context).RequestToken!);
        app.MapPost("/form", async (HttpContext context, IAntiforgery antiforgery) =>
            await antiforgery.IsRequestValidAsync(context) ? Results.Ok() : Results.BadRequest()).DisableAntiforgery();
        await app.StartAsync(Ct);
        try
        {
            // Cookies are sent by hand, in a browser's order: the site's (path /site) first, the stale one (path /) last
            using var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false })
            {
                BaseAddress = new Uri(app.Services.GetRequiredService<IServer>().Features
                    .Get<IServerAddressesFeature>()!.Addresses.First())
            };

            // The sign-in cookie is written at the path base
            var signIn = await client.PostAsync("/site/signin", null, Ct);
            var signInCookie = SetCookie(signIn, ".AspNetCore.Identity.Application");
            Assert.Contains("path=/site", signInCookie, StringComparison.OrdinalIgnoreCase);

            using var who = new HttpRequestMessage(HttpMethod.Get, "/site/who");
            who.Headers.Add("Cookie", $"{NameValue(signInCookie)}; .AspNetCore.Identity.Application={StaleValue}");
            Assert.Equal("ada", await (await client.SendAsync(who, Ct)).Content.ReadAsStringAsync(Ct));

            // The antiforgery cookie too: the form token is checked against the site's cookie, not the stale one
            var form = await client.GetAsync("/site/form", Ct);
            var antiforgeryCookie = SetCookie(form, ".AspNetCore.Antiforgery.");
            Assert.Contains("path=/site", antiforgeryCookie, StringComparison.OrdinalIgnoreCase);
            var field = app.Services.GetRequiredService<Microsoft.Extensions.Options.IOptions<AntiforgeryOptions>>()
                .Value.FormFieldName;
            using var post = new HttpRequestMessage(HttpMethod.Post, "/site/form")
            {
                Content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    [field] = await form.Content.ReadAsStringAsync(Ct)
                })
            };
            var antiforgeryName = NameValue(antiforgeryCookie).Split('=')[0];
            post.Headers.Add("Cookie", $"{NameValue(antiforgeryCookie)}; {antiforgeryName}={StaleValue}");
            Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(post, Ct)).StatusCode);
        }
        finally
        {
            await app.StopAsync(Ct);
        }
    }

    [Fact]
    public async Task ZA252_the_whole_pipeline_reads_the_sites_antiforgery_cookie_under_a_path_base()
    {
        // AddZauberCms<T> itself, so the middleware's place in front of the installer is pinned: a site with no database
        // answers with the installer, whose form posts are checked against the antiforgery cookie
        var contentRoot = TestSite.CreateContentRoot();
        await using var app = TestSite.BuildApp("", "", contentRoot, validateContainer: false,
            new Dictionary<string, string?> { ["urls"] = "http://127.0.0.1:0" });
        // The path base as UsePathBase sets it (UsePathBase itself runs routing again, which finds this test
        // assembly's own catch-all routes)
        app.Use((context, next) =>
        {
            if (context.Request.Path.StartsWithSegments("/site", out var rest))
            {
                context.Request.PathBase = "/site";
                context.Request.Path = rest;
            }

            return next(context);
        });
        app.AddZauberCms<HeadOutlet>();
        await app.StartAsync(Ct);
        try
        {
            using var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false })
            {
                BaseAddress = new Uri(app.Services.GetRequiredService<IServer>().Features
                    .Get<IServerAddressesFeature>()!.Addresses.First())
            };

            var page = await client.GetAsync("/site/install", Ct);
            Assert.Equal(HttpStatusCode.OK, page.StatusCode);
            var antiforgeryCookie = SetCookie(page, ".AspNetCore.Antiforgery.");
            Assert.Contains("path=/site", antiforgeryCookie, StringComparison.OrdinalIgnoreCase);
            var field = app.Services.GetRequiredService<Microsoft.Extensions.Options.IOptions<AntiforgeryOptions>>()
                .Value.FormFieldName;
            var html = await page.Content.ReadAsStringAsync(Ct);
            var marker = $"name=\"{field}\" value=\"";
            var start = html.IndexOf(marker, StringComparison.Ordinal) + marker.Length;

            // A post the installer refuses for what it holds (no provider chosen), not for its token
            using var post = new HttpRequestMessage(HttpMethod.Post, "/site/install")
            {
                Content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    [field] = html[start..html.IndexOf('"', start)],
                    ["step"] = "database"
                })
            };
            var antiforgeryName = NameValue(antiforgeryCookie).Split('=')[0];
            post.Headers.Add("Cookie", $"{NameValue(antiforgeryCookie)}; {antiforgeryName}={StaleValue}");
            var posted = await (await client.SendAsync(post, Ct)).Content.ReadAsStringAsync(Ct);

            // The installer's answer to a refused token; with the stale cookie read it was this one
            Assert.Contains("<form", posted, StringComparison.Ordinal);
            Assert.DoesNotContain("open for too long", posted, StringComparison.Ordinal);
        }
        finally
        {
            await app.StopAsync(Ct);
        }
    }

    [Fact]
    public async Task ZA294_a_cookies_feature_the_site_set_is_kept_which_turns_the_rule_off()
    {
        var context = new DefaultHttpContext();
        context.Request.PathBase = "/site";
        context.Request.Headers.Cookie = "a=site; a=stale";
        var own = new SiteCookiesFeature(context.Features);
        context.Features.Set<IRequestCookiesFeature>(own);
        string? read = null;

        await new PathBaseCookiesMiddleware(ctx =>
        {
            read = ctx.Request.Cookies["a"];
            return Task.CompletedTask;
        }).InvokeAsync(context);

        Assert.Same(own, context.Features.Get<IRequestCookiesFeature>());
        Assert.Equal("stale", read);
    }

    /// <summary>A site's own cookies feature: here, the framework's rule (the last cookie of a name).</summary>
    private sealed class SiteCookiesFeature(IFeatureCollection features) : IRequestCookiesFeature
    {
        private readonly RequestCookiesFeature _framework = new(features);

        public IRequestCookieCollection Cookies
        {
            get => _framework.Cookies;
            set => _framework.Cookies = value;
        }
    }

    [Theory]
    [InlineData("/site", "path=/site")]
    [InlineData("", "path=/")]
    public async Task ZA290_the_status_message_cookie_is_written_and_deleted_at_the_sites_path_base(string pathBase,
        string path)
    {
        // It was written at "/" whatever the path base, so it reached every site on the host for its five seconds.
        // The typed overload writes the message's type beside it (ZA-280), with the same builder.
        var written = new DefaultHttpContext();
        written.Request.PathBase = pathBase;
        var redirect = new IdentityRedirectManager(new FixedNavigationManager($"https://site.test{pathBase}/"));
        // Outside static rendering NavigateTo does not end the render, so the redirect throws after it
        Assert.Throws<InvalidOperationException>(new Action(() =>
            redirect.RedirectToWithStatus("account/manage", "Saved", ZauberCMS.Core.Shared.Models.ResultMessageType.Warning, written)));
        var setCookies = written.Response.Headers.SetCookie.ToArray();
        Assert.Equal(2, setCookies.Length);
        Assert.Contains(setCookies, c => c!.StartsWith($"{IdentityRedirectManager.StatusCookieName}=Saved", StringComparison.Ordinal));
        Assert.Contains(setCookies, c => c!.StartsWith($"{IdentityRedirectManager.StatusTypeCookieName}=Warning", StringComparison.Ordinal));
        Assert.All(setCookies, c => Assert.Contains(path + ";", c + ";", StringComparison.OrdinalIgnoreCase));

        // The account page that shows it deletes both at the same path
        var shown = new DefaultHttpContext();
        shown.Request.PathBase = pathBase;
        shown.Request.Headers.Cookie = $"{IdentityRedirectManager.StatusCookieName}=Saved; " +
                                       $"{IdentityRedirectManager.StatusTypeCookieName}=Warning";
        // The site's services: the account components inject the ZauberCMS services
        await using var site = await TestSite.CreateAsync(createUser: false);
        using var scope = site.CreateScope();
        await using var renderer = new HtmlRenderer(scope.ServiceProvider,
            scope.ServiceProvider.GetRequiredService<ILoggerFactory>());
        var html = await renderer.Dispatcher.InvokeAsync(async () =>
            (await renderer.RenderComponentAsync<CascadingValue<HttpContext>>(ParameterView.FromDictionary(
                new Dictionary<string, object?>
                {
                    [nameof(CascadingValue<HttpContext>.Value)] = shown,
                    [nameof(CascadingValue<HttpContext>.ChildContent)] = (RenderFragment)(builder =>
                    {
                        builder.OpenComponent<ZauberCMS.Components.Account.Shared.StatusMessage>(0);
                        builder.CloseComponent();
                    })
                }))).ToHtmlString());

        Assert.Contains("Saved", html, StringComparison.Ordinal);
        Assert.Contains("alert-warning", html, StringComparison.Ordinal);
        var deleted = shown.Response.Headers.SetCookie.ToArray();
        Assert.Equal(2, deleted.Length);
        Assert.Contains(deleted, c => c!.StartsWith($"{IdentityRedirectManager.StatusCookieName}=;", StringComparison.Ordinal));
        Assert.Contains(deleted, c => c!.StartsWith($"{IdentityRedirectManager.StatusTypeCookieName}=;", StringComparison.Ordinal));
        Assert.All(deleted, c => Assert.Contains(path + ";", c + ";", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>A navigation manager at a fixed address, as a static page has one.</summary>
    private sealed class FixedNavigationManager : NavigationManager
    {
        public FixedNavigationManager(string baseUri) => Initialize(baseUri, baseUri);

        protected override void NavigateToCore(string uri, NavigationOptions options)
        {
        }
    }

    private static string SetCookie(HttpResponseMessage response, string namePrefix) =>
        response.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith(namePrefix, StringComparison.Ordinal));

    private static string NameValue(string setCookie) => setCookie.Split(';')[0];
}
