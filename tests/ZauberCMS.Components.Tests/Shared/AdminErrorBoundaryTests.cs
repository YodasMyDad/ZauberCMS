using System.Reflection;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Server;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;
using ZauberCMS.Components.Shared;
using ZauberCMS.Core.Content.Interfaces;
using ZauberCMS.Core.Data.Interfaces;
using ZauberCMS.Core.Email.Interfaces;
using ZauberCMS.Core.Languages.Interfaces;
using ZauberCMS.Core.Media.Interfaces;
using ZauberCMS.Core.Membership.Interfaces;
using ZauberCMS.Core.Seo.Interfaces;
using ZauberCMS.Core.Tags.Interfaces;

namespace ZauberCMS.Components.Tests.Shared;

/// <summary>An editor, dashboard or page that throws shows its error in place instead of ending the circuit
/// (ZA-097, Roadmap D32).</summary>
public class AdminErrorBoundaryTests
{
    [Fact]
    public async Task ZA097_A_child_that_throws_is_replaced_by_the_error_and_logged()
    {
        var logged = new List<Exception>();

        var html = await RenderAsync(typeof(Thrower), "The Title editor", detailedErrors: false, logged);

        Assert.Contains("The Title editor could not be shown", html);
        Assert.Contains("The details are in the site&#x27;s log.", html);
        Assert.DoesNotContain("Secret connection detail", html);
        Assert.Contains("Try again", html);
        Assert.Equal("Secret connection detail", Assert.Single(logged).Message);
    }

    [Fact]
    public async Task ZA097_With_detailed_errors_on_the_message_is_shown()
    {
        var html = await RenderAsync(typeof(Thrower), null, detailedErrors: true, []);

        Assert.Contains("This part of the page could not be shown", html);
        Assert.Contains("InvalidOperationException: Secret connection detail", html);
    }

    [Fact]
    public async Task ZA097_A_child_that_works_is_rendered_as_it_is()
    {
        var html = await RenderAsync(typeof(Fine), "The Title editor", detailedErrors: false, []);

        Assert.Equal("<p>fine</p>", html.Trim());
    }

    private static async Task<string> RenderAsync(Type child, string? name, bool detailedErrors, List<Exception> logged)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.Configure<CircuitOptions>(options => options.DetailedErrors = detailedErrors);
        services.AddSingleton<IErrorBoundaryLogger>(new RecordingLogger(logged));
        services.AddSingleton<IJSRuntime, NoJsRuntime>();
        // Every component in the package gets these from _Imports.razor; the boundary uses none of them
        services.AddSingleton<NavigationManager, TestNavigationManager>();
        foreach (var type in new[]
                 {
                     typeof(IContentService), typeof(IMembershipService), typeof(IMediaService), typeof(ILanguageService),
                     typeof(ITagService), typeof(ISeoService), typeof(IEmailService), typeof(IDataService)
                 })
        {
            services.AddSingleton(type, Unused.Create(type));
        }

        await using var provider = services.BuildServiceProvider();
        await using var renderer = new HtmlRenderer(provider, NullLoggerFactory.Instance);

        return await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var output = await renderer.RenderComponentAsync<AdminErrorBoundary>(ParameterView.FromDictionary(
                new Dictionary<string, object?>
                {
                    [nameof(AdminErrorBoundary.Name)] = name,
                    [nameof(AdminErrorBoundary.ChildContent)] = (RenderFragment)(builder =>
                    {
                        builder.OpenComponent(0, child);
                        builder.CloseComponent();
                    })
                }));
            return output.ToHtmlString();
        });
    }

    private sealed class Thrower : ComponentBase
    {
        protected override void OnInitialized() => throw new InvalidOperationException("Secret connection detail");
    }

    private sealed class Fine : ComponentBase
    {
        protected override void BuildRenderTree(RenderTreeBuilder builder) => builder.AddMarkupContent(0, "<p>fine</p>");
    }

    private sealed class RecordingLogger(List<Exception> logged) : IErrorBoundaryLogger
    {
        public ValueTask LogErrorAsync(Exception exception)
        {
            logged.Add(exception);
            return ValueTask.CompletedTask;
        }
    }

    private sealed class TestNavigationManager : NavigationManager
    {
        public TestNavigationManager() => Initialize("https://localhost/", "https://localhost/admin");
    }

    /// <summary>A service that is injected but must not be called.</summary>
    public class Unused : DispatchProxy
    {
        public static object Create(Type serviceType) =>
            typeof(DispatchProxy).GetMethod(nameof(DispatchProxy.Create), 2, Type.EmptyTypes)!
                .MakeGenericMethod(serviceType, typeof(Unused)).Invoke(null, null)!;

        protected override object Invoke(MethodInfo? targetMethod, object?[]? args) =>
            throw new InvalidOperationException($"{targetMethod?.Name} was not expected to be called.");
    }

    private sealed class NoJsRuntime : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => default;
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) => default;
    }
}
