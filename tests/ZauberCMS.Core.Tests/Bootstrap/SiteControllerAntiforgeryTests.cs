using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.Extensions.DependencyInjection;
using ZauberCMS.Core.Tests.Infrastructure;

namespace ZauberCMS.Core.Tests.Bootstrap;

/// <summary>
/// ZA-185 (Roadmap D60): a site's or plugin's controller can use MVC's antiforgery attributes. Their filters come
/// with MVC's view features; with <c>AddControllers</c> alone (Phase 11e) such an action answered 500.
/// </summary>
public class SiteControllerAntiforgeryTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ZA185_ValidateAntiForgeryToken_answers_400_without_a_token_and_runs_the_action_with_one()
    {
        await using var site = await StartAsync();

        var withoutToken = await site.Client.PostAsync("/test/antiforgery/validate", Form(), Ct);
        Assert.Equal(HttpStatusCode.BadRequest, withoutToken.StatusCode);

        var withToken = await site.Client.PostAsync("/test/antiforgery/validate", Form(await site.TokenAsync()), Ct);
        Assert.Equal(HttpStatusCode.OK, withToken.StatusCode);
        Assert.Equal("validated", await withToken.Content.ReadAsStringAsync(Ct));
    }

    [Fact]
    public async Task ZA185_AutoValidateAntiforgeryToken_checks_posts_and_IgnoreAntiforgeryToken_opts_an_action_out()
    {
        await using var site = await StartAsync();

        var withoutToken = await site.Client.PostAsync("/test/antiforgery-auto/post", Form(), Ct);
        Assert.Equal(HttpStatusCode.BadRequest, withoutToken.StatusCode);

        var withToken = await site.Client.PostAsync("/test/antiforgery-auto/post", Form(await site.TokenAsync()), Ct);
        Assert.Equal(HttpStatusCode.OK, withToken.StatusCode);

        var ignored = await site.Client.PostAsync("/test/antiforgery-auto/ignored", Form(), Ct);
        Assert.Equal(HttpStatusCode.OK, ignored.StatusCode);
        Assert.Equal("ignored", await ignored.Content.ReadAsStringAsync(Ct));

        // A GET is never checked by the auto attribute
        Assert.Equal("read", await site.Client.GetStringAsync("/test/antiforgery-auto/read", Ct));
    }

    private static FormUrlEncodedContent Form((string Name, string Value)? token = null)
    {
        var fields = new Dictionary<string, string> { ["name"] = "value" };
        if (token is { } t)
        {
            fields[t.Name] = t.Value;
        }

        return new FormUrlEncodedContent(fields);
    }

    private static async Task<RunningSite> StartAsync()
    {
        var contentRoot = TestSite.CreateContentRoot();
        var app = TestSite.BuildApp("", "", contentRoot, validateContainer: false,
            new Dictionary<string, string?> { ["urls"] = "http://127.0.0.1:0" });

        // A site's Web SDK build names its own assembly as an application part; this test assembly is not one
        var parts = app.Services.GetRequiredService<ApplicationPartManager>().ApplicationParts;
        var testAssembly = typeof(AntiforgeryTestController).Assembly;
        if (!parts.OfType<AssemblyPart>().Any(p => p.Assembly == testAssembly))
        {
            parts.Add(new AssemblyPart(testAssembly));
        }

        app.UseRouting();
        app.UseAntiforgery();
        app.MapControllers();
        await app.StartAsync(Ct);

        var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!
            .Addresses.First();
        var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false, CookieContainer = new CookieContainer() })
        {
            BaseAddress = new Uri(address)
        };
        return new RunningSite(app, client, contentRoot);
    }

    private sealed class RunningSite(WebApplication app, HttpClient client, string contentRoot) : IAsyncDisposable
    {
        public HttpClient Client { get; } = client;

        /// <summary>A request token from the site; its cookie goes into the client's cookie container.</summary>
        public async Task<(string Name, string Value)> TokenAsync()
        {
            using var document = JsonDocument.Parse(await Client.GetStringAsync("/test/antiforgery/token", Ct));
            return (document.RootElement.GetProperty("formFieldName").GetString()!,
                document.RootElement.GetProperty("requestToken").GetString()!);
        }

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await app.StopAsync(Ct);
            await app.DisposeAsync();
            Directory.Delete(contentRoot, recursive: true);
        }
    }
}

/// <summary>A controller as a site or plugin writes one (ZA-185).</summary>
[Route("test/antiforgery")]
public sealed class AntiforgeryTestController(IAntiforgery antiforgery) : ControllerBase
{
    [HttpGet("token")]
    public IActionResult Token()
    {
        var tokens = antiforgery.GetAndStoreTokens(HttpContext);
        return Ok(new { tokens.FormFieldName, tokens.RequestToken });
    }

    [HttpPost("validate")]
    [ValidateAntiForgeryToken]
    public IActionResult Validate() => Content("validated");
}

/// <summary>Every unsafe method checked, one action opted out (ZA-185).</summary>
[Route("test/antiforgery-auto")]
[AutoValidateAntiforgeryToken]
public sealed class AutoAntiforgeryTestController : ControllerBase
{
    [HttpPost("post")]
    public IActionResult Post() => Content("validated");

    [HttpPost("ignored")]
    [IgnoreAntiforgeryToken]
    public IActionResult Ignored() => Content("ignored");

    [HttpGet("read")]
    public IActionResult Read() => Content("read");
}
