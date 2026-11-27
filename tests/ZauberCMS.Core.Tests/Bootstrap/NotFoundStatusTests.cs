using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ZauberCMS.Core.Extensions;
using ZauberCMS.Core.Middleware;

namespace ZauberCMS.Core.Tests.Bootstrap;

/// <summary>
/// ZA-164 (Roadmap D41): a not-found view rendered by Blazor's static rendering is sent with status 404 and its HTML.
/// .NET 10 sends a static render that ends at 404 without its HTML (left to status-code pages), so the status is
/// set as the response starts. Over real HTTP (Kestrel on a free port), because the body is dropped by Blazor's
/// endpoint and only shows on the wire.
/// </summary>
public class NotFoundStatusTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_not_found_view_is_sent_with_status_404_and_its_html()
    {
        await using var app = await StartAsync();

        var response = await app.Client.GetAsync("/when-response-starts", Ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Contains(ProbePage.Marker, await response.Content.ReadAsStringAsync(Ct));
        Assert.StartsWith("text/html", response.Content.Headers.ContentType?.ToString());
    }

    [Fact]
    public async Task A_404_set_during_the_render_is_sent_without_html_which_is_why_the_status_is_set_later()
    {
        // The framework behaviour behind ZA-164. If a .NET update changes it, setting the status when the response
        // starts is no longer needed (and this test says so).
        await using var app = await StartAsync();

        var response = await app.Client.GetAsync("/set-now", Ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Empty(await response.Content.ReadAsStringAsync(Ct));
    }

    [Fact]
    public async Task A_redirect_after_the_not_found_status_keeps_its_own_status()
    {
        await using var app = await StartAsync();

        var response = await app.Client.GetAsync("/then-redirect", Ct);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.EndsWith("/elsewhere", response.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task An_error_after_the_not_found_status_still_gets_the_error_page_with_status_500()
    {
        await using var app = await StartAsync(withErrorPage: true);

        var response = await app.Client.GetAsync("/then-throw", Ct);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Contains(ProbePage.ErrorMarker, await response.Content.ReadAsStringAsync(Ct));
    }

    [Fact]
    public async Task A_page_that_is_found_is_left_at_200()
    {
        await using var app = await StartAsync();

        var response = await app.Client.GetAsync("/found", Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(ProbePage.Marker, await response.Content.ReadAsStringAsync(Ct));
    }

    /// <summary>A Blazor app with only <see cref="ProbePage"/>, served by Kestrel on a free local port.</summary>
    private static async Task<RunningApp> StartAsync(bool withErrorPage = false)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = "Testing",
            ContentRootPath = AppContext.BaseDirectory
        });
        builder.WebHost.UseKestrel().UseUrls("http://127.0.0.1:0");
        builder.Logging.ClearProviders();
        builder.Services.AddRazorComponents();

        var app = builder.Build();
        if (withErrorPage)
        {
            // As AddZauberCms sets it up outside Development; the probe page answers /error as the site's error page
            app.UseMiddleware<ErrorPageMiddleware>();
            app.UseExceptionHandler(new ExceptionHandlerOptions
            {
                ExceptionHandlingPath = "/error", CreateScopeForErrors = true
            });
        }

        // Routing after the exception handler, as in AddZauberCms, so the error page's address is routed again
        app.UseRouting();
        app.UseAntiforgery();
        app.MapRazorComponents<ProbePage>();
        await app.StartAsync(Ct);

        var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses
            .First();
        var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false })
        {
            BaseAddress = new Uri(address)
        };
        return new RunningApp(app, client);
    }

    private sealed record RunningApp(WebApplication App, HttpClient Client) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await App.StopAsync();
            await App.DisposeAsync();
        }
    }
}

/// <summary>
/// The root component and only page of the probe app (static rendering), standing in for a site's not-found view.
/// What it does depends on the address.
/// </summary>
[Route("/{**slug}")]
public sealed class ProbePage : ComponentBase
{
    public const string Marker = "the not-found view";
    public const string ErrorMarker = "the error page";

    [CascadingParameter] public HttpContext? HttpContext { get; set; }
    [Inject] public NavigationManager NavigationManager { get; set; } = null!;

    protected override void OnInitialized()
    {
        var context = HttpContext!;
        switch (context.Request.Path.Value)
        {
            case "/when-response-starts":
                context.SetStatusCodeWhenResponseStarts(StatusCodes.Status404NotFound);
                break;
            case "/set-now":
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                break;
            case "/then-redirect":
                context.SetStatusCodeWhenResponseStarts(StatusCodes.Status404NotFound);
                NavigationManager.NavigateTo("/elsewhere");
                break;
            case "/then-throw":
                context.SetStatusCodeWhenResponseStarts(StatusCodes.Status404NotFound);
                throw new InvalidOperationException("the view failed");
        }
    }

    protected override void BuildRenderTree(RenderTreeBuilder builder) =>
        builder.AddMarkupContent(0, $"<!DOCTYPE html><html><body><p>{(
            HttpContext?.Request.Path == "/error" ? ErrorMarker : Marker)}</p></body></html>");
}
