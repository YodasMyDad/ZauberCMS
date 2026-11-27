using System.Net;
using System.Reflection;
using System.Text.RegularExpressions;
using Blazored.Modal.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.AspNetCore.Razor.Hosting;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.DependencyInjection;
using Radzen;
using ZauberCMS.Components.ContextMenus.SectionNavGroups;
using ZauberCMS.Components.Editors;
using ZauberCMS.Core.Content.Interfaces;
using ZauberCMS.Core.Media.Services;
using ZauberCMS.Core.Plugins;
using ZauberCMS.Core.Sections.Interfaces;
using ZauberCMS.Core.Shared.Services;
using ZauberCMS.Core.Tests.Infrastructure;

namespace ZauberCMS.Core.Tests.Bootstrap;

/// <summary>
/// Roadmap Phase 11e. ZA-090: the unwired MVC rendering path is gone, and what it registered that live code uses is
/// registered on its own. ZA-091 (D51): Core has no UI library in its contracts; the admin UI contracts, the vendored
/// modal and their services are in ZauberCMS.Components under the full names they had.
/// </summary>
public class CoreSurfaceTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static readonly Assembly Core = typeof(ZauberSetup).Assembly;
    private static readonly Assembly Components = typeof(TextProperty).Assembly;
    private static readonly Assembly Routing = Assembly.Load(new AssemblyName("ZauberCMS.Routing"));

    // ---- ZA-125 ----

    [Fact]
    public void ZA125_Content_Url_is_the_stored_url_and_the_Url_extension_is_the_obsolete_one()
    {
        var property = typeof(ZauberCMS.Core.Content.Models.Content).GetProperty("Url")!;
        Assert.Null(property.GetCustomAttribute<ObsoleteAttribute>());
        Assert.NotNull(property.SetMethod);

        var extension = typeof(ZauberCMS.Core.Extensions.ContentExtensions).GetMethod("Url")!;
        Assert.Contains("Content.Url", extension.GetCustomAttribute<ObsoleteAttribute>()?.Message);
    }

    [Fact]
    public void ZA125_the_split_services_keep_their_public_shape()
    {
        // Partial classes: a site that subclasses or decorates them sees the same type, constructor and members
        foreach (var (service, contract) in new[]
                 {
                     (typeof(ZauberCMS.Core.Content.Services.ContentService), typeof(IContentService)),
                     (typeof(ZauberCMS.Core.Membership.Services.MembershipService),
                         typeof(ZauberCMS.Core.Membership.Interfaces.IMembershipService))
                 })
        {
            Assert.True(service.IsPublic && !service.IsSealed);
            Assert.Single(service.GetConstructors());
            foreach (var method in contract.GetMethods())
            {
                Assert.NotNull(service.GetMethod(method.Name, method.GetParameters().Select(p => p.ParameterType).ToArray()));
            }
        }
    }

    // ---- ZA-090 ----

    [Fact]
    public void ZA090_the_mvc_rendering_path_is_not_shipped()
    {
        string[] removed =
        [
            "ZauberCMS.Core.Shared.ZauberRouteValueTransformer",
            "ZauberCMS.Core.Content.ContentFinders.ContentFinderPipeline",
            "ZauberCMS.Core.Content.ContentFinders.DefaultContentFinder",
            "ZauberCMS.Core.Rendering.IZauberViewPage",
            "ZauberCMS.Core.Rendering.ZauberPageViewModel",
            "ZauberCMS.Core.Rendering.ZauberViewPage`1",
            "ZauberCMS.Core.Extensions.HtmlHelperExtensions",
            "ZauberCMS.Core.Extensions.RenderExtensions",
        ];
        Assert.DoesNotContain(removed, name => Core.GetType(name) != null);
        // The name IContentFinder is used again for the content finders of Roadmap D55, without the MVC route values
        Assert.DoesNotContain(typeof(ZauberCMS.Core.Content.Interfaces.IContentFinder).GetMethods(),
            m => m.ReturnType.ToString().Contains("RouteValueDictionary", StringComparison.Ordinal));

        string[] mvcBaseTypes =
        [
            "Microsoft.AspNetCore.Mvc.Razor.RazorPage",
            "Microsoft.AspNetCore.Mvc.ViewComponent",
            "Microsoft.AspNetCore.Mvc.Routing.DynamicRouteValueTransformer",
        ];
        foreach (var assembly in new[] { Core, Components, Routing })
        {
            var mvcTypes = assembly.GetTypes().Where(t => BaseTypes(t).Any(b => mvcBaseTypes.Contains(b.FullName)));
            Assert.Empty(mvcTypes.Select(t => t.FullName));

            // A compiled .cshtml view is an assembly attribute of kind mvc.1.0.view
            Assert.DoesNotContain(assembly.GetCustomAttributes<RazorCompiledItemAttribute>(),
                a => a.Kind.StartsWith("mvc.", StringComparison.Ordinal));
        }

        // Only that path read IContentBlockView.RenderMode (an MVC type)
        Assert.Empty(typeof(IContentBlockView).GetProperties());
    }

    [Fact]
    public async Task ZA090_the_caches_are_registered_and_the_mvc_view_engine_is_not()
    {
        var contentRoot = TestSite.CreateContentRoot();
        try
        {
            await using var app = TestSite.BuildApp("Sqlite", "DataSource=:memory:", contentRoot, validateContainer: true);
            var registered = app.Services.GetRequiredService<IServiceProviderIsService>();

            // ServerCache, the confirmation re-sends and the form tokens use IMemoryCache; MVC's views brought it before
            Assert.NotNull(app.Services.GetRequiredService<IMemoryCache>());
            Assert.IsType<MemoryDistributedCache>(app.Services.GetRequiredService<IDistributedCache>());
            Assert.False(registered.IsService(typeof(IRazorViewEngine)));

            // With Redis configured, its IDistributedCache is not replaced by the memory one (no connection is opened)
            await using var withRedis = TestSite.BuildApp("Sqlite", "DataSource=:memory:", contentRoot,
                validateContainer: false,
                new Dictionary<string, string?> { ["Zauber:RedisConnectionString"] = "127.0.0.1:1,abortConnect=false" });
            Assert.IsAssignableFrom<RedisCache>(withRedis.Services.GetRequiredService<IDistributedCache>());
        }
        finally
        {
            Directory.Delete(contentRoot, recursive: true);
        }
    }

    // [ValidateAntiForgeryToken] needs MVC's view services; the sign-out checks the token itself now
    [Fact]
    public async Task ZA090_the_sign_out_post_checks_its_antiforgery_token()
    {
        await using var site = await TestSite.CreateAsync(
            new Dictionary<string, string?> { ["urls"] = "http://127.0.0.1:0" }, createUser: false);
        var app = site.App;
        // A site's Web SDK build names ZauberCMS.Components as an application part; this test assembly is not one
        var parts = app.Services.GetRequiredService<ApplicationPartManager>().ApplicationParts;
        if (!parts.OfType<AssemblyPart>().Any(p => p.Assembly == Components))
        {
            parts.Add(new AssemblyPart(Components));
        }

        app.UseRouting();
        app.UseAntiforgery();
        app.MapControllers();
        await app.StartAsync(Ct);
        try
        {
            var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!
                .Addresses.First();
            var cookies = new CookieContainer();
            using var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false, CookieContainer = cookies })
            {
                BaseAddress = new Uri(address)
            };

            var withoutToken = await client.PostAsync("/api/auth/logout",
                new FormUrlEncodedContent(new Dictionary<string, string> { ["redirectUrl"] = "/" }), Ct);
            Assert.Equal(HttpStatusCode.BadRequest, withoutToken.StatusCode);

            // The GET page carries the token (and sets its cookie); posting its form signs out and redirects
            var page = await client.GetStringAsync("/api/auth/logout", Ct);
            var field = Regex.Match(page, "name=\"(?<name>[^\"]+)\" value=\"(?<value>[^\"]+)\"");
            Assert.True(field.Success, page);
            var withToken = await client.PostAsync("/api/auth/logout", new FormUrlEncodedContent(
                new Dictionary<string, string>
                {
                    [WebUtility.HtmlDecode(field.Groups["name"].Value)] = WebUtility.HtmlDecode(field.Groups["value"].Value),
                    ["redirectUrl"] = "/"
                }), Ct);
            Assert.Equal(HttpStatusCode.Redirect, withToken.StatusCode);
        }
        finally
        {
            await app.StopAsync(Ct);
        }
    }

    // ---- ZA-091 ----

    [Fact]
    public void ZA091_core_does_not_reference_the_ui_libraries()
    {
        var references = Core.GetReferencedAssemblies().Select(a => a.Name).ToList();
        Assert.DoesNotContain("Radzen.Blazor", references);
        Assert.DoesNotContain("ZauberCMS.Components", references);
        Assert.Empty(Core.GetTypes().Where(t => t.Namespace?.StartsWith("Blazored", StringComparison.Ordinal) == true)
            .Select(t => t.FullName));
    }

    [Theory]
    [InlineData("ZauberCMS.Core.Content.Interfaces.IContentProperty")]
    [InlineData("ZauberCMS.Core.Sections.Interfaces.ISectionNavGroupAction")]
    [InlineData("ZauberCMS.Core.Extensions.DialogExtensions")]
    [InlineData("ZauberCMS.Core.Extensions.NotificationServiceExtensions")]
    [InlineData("ZauberCMS.Core.Extensions.TreeExtensions")]
    [InlineData("ZauberCMS.Core.Extensions.ContentPropertyExtensions")]
    [InlineData("ZauberCMS.Core.Media.Services.MediaValidationService")]
    [InlineData("ZauberCMS.Core.Shared.Services.ValidateService`1")]
    [InlineData("Blazored.Modal.BlazoredModal")]
    [InlineData("Blazored.Modal.BlazoredModalInstance")]
    [InlineData("Blazored.Modal.CascadingBlazoredModal")]
    [InlineData("Blazored.Modal.ModalOptions")]
    [InlineData("Blazored.Modal.ModalParameters")]
    [InlineData("Blazored.Modal.IModalReference")]
    [InlineData("Blazored.Modal.Services.IModalService")]
    [InlineData("Blazored.Modal.Services.ModalResult")]
    public void ZA091_a_moved_type_keeps_its_full_name_in_components(string fullName)
    {
        Assert.NotNull(Components.GetType(fullName));
        Assert.True(Components.GetType(fullName)!.IsPublic, $"{fullName} is not public");
        Assert.Null(Core.GetType(fullName));
    }

    [Fact]
    public async Task ZA091_components_registers_the_admin_ui_services_and_discovery_finds_the_moved_contracts()
    {
        var contentRoot = TestSite.CreateContentRoot();
        try
        {
            await using var app = TestSite.BuildApp("Sqlite", "DataSource=:memory:", contentRoot, validateContainer: true);
            using var scope = app.Services.CreateScope();
            var services = scope.ServiceProvider;

            // Radzen's dialog and context menu services need a circuit's NavigationManager to be built
            var registered = services.GetRequiredService<IServiceProviderIsService>();
            Assert.True(registered.IsService(typeof(DialogService)));
            Assert.True(registered.IsService(typeof(ContextMenuService)));
            Assert.NotNull(services.GetRequiredService<NotificationService>());
            Assert.NotNull(services.GetRequiredService<IModalService>());
            Assert.NotNull(services.GetRequiredService<ValidateService<ZauberCMS.Core.Content.Models.Content>>());
            Assert.NotNull(services.GetRequiredService<MediaValidationService>());

            var extensionManager = services.GetRequiredService<ExtensionManager>();
            Assert.Contains(typeof(TextProperty).FullName!, extensionManager.GetImplementations<IContentProperty>().Keys);
            Assert.Contains(typeof(CreateContentNavMenu).FullName!,
                extensionManager.GetImplementations<ISectionNavGroupAction>().Keys);
        }
        finally
        {
            Directory.Delete(contentRoot, recursive: true);
        }
    }

    private static IEnumerable<Type> BaseTypes(Type type)
    {
        for (var t = type.BaseType; t != null; t = t.BaseType)
        {
            yield return t.IsGenericType ? t.GetGenericTypeDefinition() : t;
        }
    }
}
