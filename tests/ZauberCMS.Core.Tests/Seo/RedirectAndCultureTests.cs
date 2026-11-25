using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Server.Circuits;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
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
        var middleware = new RedirectMiddleware(_ => Task.CompletedTask, NullLogger<RedirectMiddleware>.Instance,
            _site.Services.GetRequiredService<IMemoryCache>());

        var context = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
        context.Request.Path = path;
        await middleware.InvokeAsync(context, scope.ServiceProvider.GetRequiredService<ISeoService>());

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
