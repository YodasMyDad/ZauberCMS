using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.Extensions.DependencyInjection;
using ZauberCMS.Core.Extensions;
using ZauberCMS.Core.Tests.Infrastructure;

namespace ZauberCMS.Core.Tests.Security;

/// <summary>
/// ZA-219 (Roadmap D101): a site under a path base (<c>UsePathBase("/site")</c>) keeps it in every navigation, link
/// and redirect ZauberCMS makes to one of its own addresses. Those addresses are site paths ("/admin/users", the
/// <see cref="Urls"/> constants, a section's IndexUrl), read from the site's root, as a route template is; return
/// addresses are paths from the host's root with the path base, as ASP.NET's cookie handler writes them.
/// </summary>
public partial class PathBaseNavigationTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("https://example.com/site/", "/admin/users", "/site/admin/users")]
    [InlineData("https://example.com/site/", "/api/auth/refreshsignin?redirectUrl=%2Fsite%2Fadmin&ticket=a%2Bb",
        "/site/api/auth/refreshsignin?redirectUrl=%2Fsite%2Fadmin&ticket=a%2Bb")]
    [InlineData("https://example.com/site/", "/", "/site/")]
    [InlineData("https://example.com/my%20site/", "/admin", "/my%20site/admin")]
    [InlineData("https://example.com/", "/admin", "/admin")]
    // Not a site path: left for NavigateTo and the browser to resolve as before
    [InlineData("https://example.com/site/", "media/1/photo.jpg", "media/1/photo.jpg")]
    [InlineData("https://example.com/site/", "https://other.example/x", "https://other.example/x")]
    [InlineData("https://example.com/site/", "//other.example/x", "//other.example/x")]
    [InlineData("https://example.com/site/", "", "")]
    public void ZA219_a_site_path_is_read_below_the_base_address(string baseUri, string url, string expected)
    {
        var navigation = new FixedNavigationManager(baseUri, baseUri);

        Assert.Equal(expected, navigation.ToSiteUrl(url));
    }

    [Theory]
    [InlineData("https://example.com/site/", "blog/my-post", "/site/blog/my-post")]
    [InlineData("https://example.com/my%20site/", "about", "/my%20site/about")]
    [InlineData("https://example.com/", "about", "/about")]
    [InlineData("https://example.com/site/", null, null)]
    public void ZA251_a_link_to_a_page_is_read_below_the_base_address(string baseUri, string? url, string? expected)
    {
        var navigation = new FixedNavigationManager(baseUri, baseUri);

        Assert.Equal(expected, navigation.ToSiteUrl(new ZauberCMS.Core.Content.Models.Content { Url = url }));
    }

    [Theory]
    // A link to a page (data-contentid) holds its site path; under a path base it is rendered below it
    [InlineData("/site", """<a href="/blog?page=2#top" data-contentid="5">Blog</a>""",
        """<a href="/site/blog?page=2#top" data-contentid="5">Blog</a>""")]
    [InlineData("/my%20site", """<a href="/" data-contentid="5">Home</a>""",
        """<a href="/my%20site/" data-contentid="5">Home</a>""")]
    // At the host's root, nothing changes
    [InlineData("", """<a href="/blog" data-contentid="5">Blog</a>""", """<a href="/blog" data-contentid="5">Blog</a>""")]
    // What an editor typed is rendered as typed, as Umbraco does; so is a page link to a full address
    [InlineData("/site", """<a href="/contact">Contact</a>""", """<a href="/contact">Contact</a>""")]
    [InlineData("/site", """<a href="https://other.example/x" data-contentid="5">X</a>""",
        """<a href="https://other.example/x" data-contentid="5">X</a>""")]
    [InlineData("/site", """<img src="media/1/photo.jpg" data-mediaid="7">""", """<img src="media/1/photo.jpg" data-mediaid="7">""")]
    public void ZA251_a_page_link_in_rich_text_is_rendered_below_the_requests_path_base(string pathBase, string html,
        string expected)
    {
        var context = new DefaultHttpContext();
        context.Request.PathBase = new PathString(Uri.UnescapeDataString(pathBase));
        var sanitizer = new ZauberCMS.Core.Shared.Services.DefaultHtmlSanitizerService(null, null,
            new HttpContextAccessor { HttpContext = context }, null);

        Assert.Equal(expected, sanitizer.Sanitize(html));
    }

    [Fact]
    public void ZA251_in_a_circuit_a_page_link_in_rich_text_is_rendered_below_its_base_address()
    {
        // A circuit, and a block preview rendered in one, has no request: its navigation manager has the base address
        var sanitizer = new ZauberCMS.Core.Shared.Services.DefaultHtmlSanitizerService(null, null,
            new HttpContextAccessor(), new FixedNavigationManager("https://example.com/site/", "https://example.com/site/"));

        Assert.Equal("""<a href="/site/blog" data-contentid="5">Blog</a>""",
            sanitizer.Sanitize("""<a href="/blog" data-contentid="5">Blog</a>"""));
    }

    [Fact]
    public void ZA251_with_no_request_or_circuit_a_page_link_is_rendered_as_stored()
    {
        // A background job's scope: the navigation manager is there but was never initialised
        var sanitizer = new ZauberCMS.Core.Shared.Services.DefaultHtmlSanitizerService(null, null,
            new HttpContextAccessor(), new UninitialisedNavigationManager());

        Assert.Equal("""<a href="/blog" data-contentid="5">Blog</a>""",
            sanitizer.Sanitize("""<a href="/blog" data-contentid="5">Blog</a>"""));
    }

    [Fact]
    public async Task ZA251_the_sanitiser_a_site_resolves_renders_page_links_below_the_path_base()
    {
        // The registration builds the sanitiser with the request's accessor, so a view's @inject gets the path base
        await using var site = await TestSite.CreateAsync();
        using var scope = site.CreateScope();
        var context = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
        context.Request.PathBase = "/site";
        scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>().HttpContext = context;

        var sanitizer = scope.ServiceProvider.GetRequiredService<ZauberCMS.Core.Shared.Services.IHtmlSanitizerService>();

        Assert.Equal("""<p><a href="/site/blog" data-contentid="5">Blog</a></p>""",
            sanitizer.Sanitize("""<p><a href="/blog" data-contentid="5">Blog</a></p>"""));
    }

    [Fact]
    public void ZA219_the_return_address_keeps_the_path_base_and_the_query()
    {
        var navigation = new FixedNavigationManager("https://example.com/site/",
            "https://example.com/site/admin/users/edit/1?tab=2");

        Assert.Equal("/site/admin/users/edit/1?tab=2", navigation.GetReturnUrl());
        Assert.Equal("/site/admin/users/editrole/5", navigation.GetReturnUrl($"{Urls.AdminUsersEditRole}/5"));
        // What the login page keeps of it: the same address
        Assert.Equal("/site/admin/users/edit/1?tab=2", navigation.ToLocalReturnUrl(navigation.GetReturnUrl()));
    }

    [Theory]
    [InlineData("/\t/evil.example")]
    [InlineData("/\r\n/evil.example")]
    [InlineData("/\v/evil.example")]
    public void ZA253_a_return_address_a_browser_reads_as_another_host_once_it_drops_tabs_and_line_breaks_is_refused(
        string returnUrl)
    {
        var navigation = new FixedNavigationManager("https://example.com/site/", "https://example.com/site/");

        Assert.Null(navigation.ToLocalReturnUrl(returnUrl));
    }

    [Theory]
    [InlineData("https://other.example/page", "")]
    [InlineData("/\t/evil.example", "")]
    [InlineData("https://example.com/site/admin?tab=1", "admin?tab=1")]
    [InlineData("/site/admin", "/site/admin")]
    public void ZA253_a_return_address_the_redirect_cannot_use_goes_to_the_home_page_instead_of_failing(string uri,
        string expected)
    {
        // ExternalLogin passed its query's return address straight on: one on another site threw ArgumentException
        // after the sign-in, so the visitor got an error page
        var navigation = new FixedNavigationManager("https://example.com/site/", "https://example.com/site/");
        var redirect = new ZauberCMS.Core.Membership.IdentityRedirectManager(navigation);

        // Outside static rendering NavigateTo does not end the render, so RedirectTo throws after navigating
        Assert.Throws<InvalidOperationException>(new Action(() => redirect.RedirectTo(uri)));

        Assert.Equal(expected, navigation.LastNavigation);
    }

    [Theory]
    [InlineData(null, "/site", "http://site.test/site/account/resetpassword")]
    [InlineData(null, "", "http://site.test/account/resetpassword")]
    [InlineData("https://example.com/site", "", "https://example.com/site/account/resetpassword")]
    [InlineData("https://example.com/site/", "/other", "https://example.com/site/account/resetpassword")]
    [InlineData("https://example.com", "", "https://example.com/account/resetpassword")]
    public void ZA219_email_links_are_below_the_path_base_or_the_site_urls_path(string? siteUrl, string pathBase,
        string expected)
    {
        var services = new ServiceCollection()
            .Configure<Settings.ZauberSettings>(s => s.SiteUrl = siteUrl)
            .BuildServiceProvider();
        var context = new DefaultHttpContext { RequestServices = services };
        context.Request.Scheme = "http";
        context.Request.Host = new HostString("site.test");
        context.Request.PathBase = pathBase;
        var accessor = new HttpContextAccessor { HttpContext = context };

        Assert.Equal(expected, accessor.ToAbsoluteUrl(Urls.Account.ResetPassword));
        Assert.Equal(expected + "?code=x", accessor.ToAbsoluteUrl(Urls.Account.ResetPassword, new { code = "x" }));
    }

    [Fact]
    public async Task ZA219_the_sign_in_refresh_and_sign_out_keep_the_path_base()
    {
        var contentRoot = TestSite.CreateContentRoot();
        await using var app = TestSite.BuildApp("Sqlite", "DataSource=:memory:", contentRoot, validateContainer: false,
            new Dictionary<string, string?> { ["urls"] = "http://127.0.0.1:0" });
        // A site's Web SDK build names ZauberCMS.Components as an application part; this test assembly is not one
        var parts = app.Services.GetRequiredService<ApplicationPartManager>().ApplicationParts;
        if (!parts.OfType<AssemblyPart>().Any(p => p.Assembly == typeof(ZauberCMS.Components.Controllers.AuthController).Assembly))
        {
            parts.Add(new AssemblyPart(typeof(ZauberCMS.Components.Controllers.AuthController).Assembly));
        }

        app.UsePathBase("/site");
        app.UseRouting();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();
        await app.StartAsync(Ct);
        try
        {
            using var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false })
            {
                BaseAddress = new Uri(app.Services.GetRequiredService<IServer>().Features
                    .Get<IServerAddressesFeature>()!.Addresses.First())
            };

            // Nobody signed in: back to the login page below the path base
            var refresh = await client.GetAsync("/site/api/auth/refreshsignin?redirectUrl=%2Fsite%2Fadmin", Ct);
            Assert.Equal(HttpStatusCode.Redirect, refresh.StatusCode);
            Assert.Equal("/site/account/login", refresh.Headers.Location?.OriginalString);

            // The sign-out form posts below the path base and goes to its home page
            var logout = await client.GetStringAsync("/site/api/auth/logout", Ct);
            Assert.Contains("action=\"/site/api/auth/logout\"", logout, StringComparison.Ordinal);
            Assert.Contains("name=\"redirectUrl\" value=\"/site/\"", logout, StringComparison.Ordinal);
        }
        finally
        {
            await app.StopAsync(Ct);
        }
    }

    [Fact]
    public void ZA219_no_package_navigates_links_or_redirects_to_a_site_path_from_the_host_root()
    {
        // NavigateTo("/admin/..."), href="/admin/..." and Redirect("/...") read the path from the host's root and leave
        // a path base: site paths go through ToSiteUrl (components) or the request's PathBase (server code). Also the
        // template's site and the sample site, which sites copy (ZA-251): href="@ContentUrl.SiteRelative(page)" too.
        var root = RepositoryRoot();
        var offenders = ScannedProjects
            .SelectMany(project => Directory.EnumerateFiles(Path.Combine(root, project), "*.*", SearchOption.AllDirectories))
            .Where(path => path.EndsWith(".cs", StringComparison.Ordinal) || path.EndsWith(".razor", StringComparison.Ordinal) ||
                           (path.EndsWith(".js", StringComparison.Ordinal) && !path.EndsWith(".min.js", StringComparison.Ordinal)) ||
                           (path.EndsWith(".css", StringComparison.Ordinal) && !path.EndsWith(".min.css", StringComparison.Ordinal)))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal) &&
                           !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal) &&
                           !path.Contains($"{Path.DirectorySeparatorChar}lib{Path.DirectorySeparatorChar}", StringComparison.Ordinal) &&
                           !path.Contains($"{Path.DirectorySeparatorChar}node_modules{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .SelectMany(path =>
            {
                var pattern = path.EndsWith(".js", StringComparison.Ordinal) ? ScriptFromHostRoot()
                    : path.EndsWith(".css", StringComparison.Ordinal) ? StyleFromHostRoot()
                    : FromHostRoot();
                // Comments may show the wrong way (a line that starts with //, ///, @* or *): blanked, lines kept
                var text = string.Join('\n', File.ReadAllLines(path).Select(line =>
                    line.TrimStart() is var code && (code.StartsWith("//", StringComparison.Ordinal) ||
                                                     code.StartsWith("@*", StringComparison.Ordinal) ||
                                                     code.StartsWith('*'))
                        ? string.Empty
                        : line));
                return pattern.Matches(text).Select(match =>
                    $"{Path.GetRelativePath(root, path)}:{text[..match.Index].Count(c => c == '\n') + 1}");
            })
            .ToList();

        Assert.True(offenders.Count == 0, "A site path from the host's root: " + string.Join(", ", offenders));
    }

    private static readonly string[] ScannedProjects =
    [
        "ZauberCMS.Core", "ZauberCMS.Components", "ZauberCMS.Routing", "ZauberCMS",
        Path.Combine("ZauberCMS.Template", "template"), "ZauberCMS.Web"
    ];

    // A call that navigates or redirects (NavigateTo, Redirect, RedirectTo, RedirectToWithStatus) with a "/…" literal
    // or a Urls constant in its arguments that no ToSiteUrl, GetReturnUrl, SitePath, HostPath or PathBase + wraps
    // (a ternary across lines too); or an href, src, srcset, action or Radzen Path attribute, also one written in a C#
    // string (\"), that starts with "/", a Urls constant or a page's ContentUrl.SiteRelative link; or a static asset
    // (Assets["/…"]), a style's url(/…) or a media fallback (GetMediaItems(…, "/…")) from the host's root (ZA-297).
    [GeneratedRegex(HostRootPattern)]
    private static partial Regex FromHostRoot();

    private const string HostRootPattern =
        """\b(NavigateTo|Redirect|RedirectTo|RedirectToWithStatus)\((?:(?!ToSiteUrl\(|GetReturnUrl\(|SitePath\(|HostPath\(|PathBase\s*\+)[^;])*?(\$?"/(?![/\\])|\$"\{(ZauberCMS\.Core\.)?Urls\.|(?<![\w.])(ZauberCMS\.Core\.)?Urls\.[A-Z])""" +
        """|\b(href|src|srcset|action|Path)=\\?["'](/(?![/\\])|@\(?\$?"/|@\(?\$?"?\{?(ZauberCMS\.Core\.)?Urls\.|@\(?(ZauberCMS\.Core\.Content\.)?ContentUrl\.SiteRelative\()""" +
        """|Assets\["/(?![/\\])|url\(\s*(&\#x27;|['"])?/(?![/\\])|GetMediaItems\([^;]*?"/(?![/\\])""";

    // In a stylesheet: url(/…) from the host's root (a font or image the page's path base does not reach)
    [GeneratedRegex("""url\(\s*['"]?/(?![/\\])""")]
    private static partial Regex StyleFromHostRoot();

    // In a script: a fetch or an address set to a path from the host's root
    [GeneratedRegex("""fetch\(\s*['"`]/(?![/\\])|location(\.href)?\s*=\s*['"`]/(?![/\\])|['"`]/(api|admin|account)/""")]
    private static partial Regex ScriptFromHostRoot();

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "ZauberCMS.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("ZauberCMS.sln not found above the test output.");
    }

    /// <summary>A navigation manager at a fixed address, as a request or a circuit gives one.</summary>
    private sealed class FixedNavigationManager : Microsoft.AspNetCore.Components.NavigationManager
    {
        public FixedNavigationManager(string baseUri, string uri) => Initialize(baseUri, uri);

        public string? LastNavigation { get; private set; }

        protected override void NavigateToCore(string uri, Microsoft.AspNetCore.Components.NavigationOptions options) =>
            LastNavigation = uri;
    }

    /// <summary>A navigation manager outside a page or circuit: never initialised.</summary>
    private sealed class UninitialisedNavigationManager : Microsoft.AspNetCore.Components.NavigationManager;
}
