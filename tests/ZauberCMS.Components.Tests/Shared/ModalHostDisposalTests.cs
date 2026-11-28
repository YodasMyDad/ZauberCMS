using System.Reflection;
using Blazored.Modal;
using Blazored.Modal.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace ZauberCMS.Components.Tests.Shared;

/// <summary>
/// The side-panel host lives in the admin layout, which an error shown in its place (ZA-215) removes and the next
/// page makes again. The removed host must stop answering the circuit's modal service and navigations, or the next
/// navigation runs it disposed and ends the circuit (ZA-197).
/// </summary>
public class ModalHostDisposalTests
{
    [Fact]
    public async Task ZA197_a_removed_modal_host_no_longer_answers_the_modal_service_or_navigation()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IJSRuntime, NoJsRuntime>();
        var navigation = new MovableNavigationManager();
        services.AddSingleton<NavigationManager>(navigation);
        var modalService = new ModalService();
        services.AddSingleton<IModalService>(modalService);
        AdminRenderer.AddImportedServices(services);
        await using var provider = services.BuildServiceProvider();

        var renderer = new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>());
        await renderer.Dispatcher.InvokeAsync(() =>
            renderer.RenderComponentAsync<CascadingBlazoredModal>(ParameterView.Empty));
        Assert.NotNull(Handlers(modalService, nameof(ModalService.OnModalInstanceAdded)));
        var open = await renderer.Dispatcher.InvokeAsync(() => modalService.Show<PanelContent>("A side panel"));

        // The layout goes, as when an error is shown in its place
        await renderer.DisposeAsync();

        // The panel that was open is cancelled, not left pending for ever
        Assert.True((await open.Result.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken)).Cancelled);
        Assert.Null(Handlers(modalService, nameof(ModalService.OnModalInstanceAdded)));
        Assert.Null(Handlers(modalService, nameof(ModalService.OnModalCloseRequested)));
        Assert.Null(Handlers(navigation, "_locationChanged"));
        navigation.MoveTo("https://localhost/admin/media");
    }

    private static Delegate? Handlers(object owner, string eventName)
    {
        for (var type = owner.GetType(); type != null; type = type.BaseType)
        {
            var field = type.GetField(eventName, BindingFlags.Instance | BindingFlags.NonPublic);
            if (field != null)
            {
                return (Delegate?)field.GetValue(owner);
            }
        }

        throw new InvalidOperationException($"No event field {eventName} on {owner.GetType()}");
    }

    private sealed class PanelContent : ComponentBase;

    private sealed class MovableNavigationManager : NavigationManager
    {
        public MovableNavigationManager() => Initialize("https://localhost/", "https://localhost/admin/content");

        public void MoveTo(string uri)
        {
            Uri = uri;
            NotifyLocationChanged(isInterceptedLink: false);
        }
    }

    private sealed class NoJsRuntime : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => default;
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) => default;
    }
}
