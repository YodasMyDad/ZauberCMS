using System.Diagnostics;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ZauberCMS.Core.Middleware;
using ZauberCMS.Core.Settings;
using ZauberCMS.Core.Tests.Infrastructure;

namespace ZauberCMS.Core.Tests.Bootstrap;

/// <summary>
/// ZA-098 (Roadmap D33): outside Development a request that throws gets the site's error page, and the built-in page
/// when the site has none or it fails as well; never an empty 500.
/// </summary>
public class ErrorPageTests
{
    [Fact]
    public async Task The_site_error_page_is_shown_with_status_500()
    {
        var context = await RunAsync(async ctx =>
        {
            if (ctx.Request.Path == "/error")
            {
                await ctx.Response.WriteAsync("the site's own error page");
                return;
            }

            throw new InvalidOperationException("boom");
        });

        Assert.Equal(StatusCodes.Status500InternalServerError, context.Response.StatusCode);
        Assert.Equal("the site's own error page", Body(context));
    }

    [Fact]
    public async Task Without_a_page_at_the_error_address_the_built_in_page_is_sent()
    {
        var context = await RunAsync(ctx =>
        {
            if (ctx.Request.Path == "/error")
            {
                ctx.Response.StatusCode = StatusCodes.Status404NotFound;
                return Task.CompletedTask;
            }

            throw new InvalidOperationException("boom");
        });

        AssertBuiltInPage(context);
    }

    [Fact]
    public async Task An_error_page_that_fails_as_well_gives_the_built_in_page()
    {
        var context = await RunAsync(_ => throw new InvalidOperationException("the database is down"));

        AssertBuiltInPage(context);
    }

    [Fact]
    public async Task A_request_that_works_is_left_alone()
    {
        var context = await RunAsync(ctx => ctx.Response.WriteAsync("fine"));

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        Assert.Equal("fine", Body(context));
    }

    [Fact]
    public void The_error_page_address_is_error_by_default() =>
        Assert.Equal("/error", new ZauberSettings().ErrorPagePath);

    [Fact]
    public async Task The_site_uses_the_error_page_setting_from_configuration()
    {
        await using var site = await TestSite.CreateAsync(new Dictionary<string, string?>
        {
            ["Zauber:ErrorPagePath"] = "/oops"
        }, createUser: false);

        Assert.Equal("/oops", site.Services.GetRequiredService<IOptions<ZauberSettings>>().Value.ErrorPagePath);
    }

    private static void AssertBuiltInPage(HttpContext context)
    {
        Assert.Equal(StatusCodes.Status500InternalServerError, context.Response.StatusCode);
        Assert.StartsWith("text/html", context.Response.ContentType);
        var body = Body(context);
        Assert.Contains("Something went wrong", body);
        Assert.Contains("Request id:", body);
        Assert.DoesNotContain("boom", body);
        Assert.DoesNotContain("database", body);
    }

    /// <summary>The same two middleware AddZauberCms puts first outside Development, in front of <paramref name="site"/>.</summary>
    private static async Task<HttpContext> RunAsync(RequestDelegate site)
    {
        var services = new ServiceCollection()
            .AddLogging()
            .AddMetrics()
            .AddOptions()
            .AddSingleton(new DiagnosticListener("ErrorPageTests"))
            .AddSingleton<DiagnosticSource>(sp => sp.GetRequiredService<DiagnosticListener>())
            .BuildServiceProvider();

        var app = new ApplicationBuilder(services);
        app.UseMiddleware<ErrorPageMiddleware>();
        app.UseExceptionHandler(new ExceptionHandlerOptions { ExceptionHandlingPath = "/error" });
        app.Run(site);
        var pipeline = app.Build();

        var context = new DefaultHttpContext { RequestServices = services };
        context.Request.Path = "/a-page";
        context.Request.Method = "GET";
        context.Response.Body = new MemoryStream();
        await pipeline(context);
        return context;
    }

    private static string Body(HttpContext context)
    {
        context.Response.Body.Position = 0;
        return new StreamReader(context.Response.Body).ReadToEnd();
    }
}
