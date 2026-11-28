using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Server;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using ZauberCMS.Core.Content.Interfaces;
using ZauberCMS.Core.Data.Interfaces;
using ZauberCMS.Core.Email.Interfaces;
using ZauberCMS.Core.Languages.Interfaces;
using ZauberCMS.Core.Media.Interfaces;
using ZauberCMS.Core.Membership.Interfaces;
using ZauberCMS.Core.Seo.Interfaces;
using ZauberCMS.Core.Tags.Interfaces;

namespace ZauberCMS.Components.Tests.Shared;

/// <summary>
/// Renders one admin component to HTML with what every component of the package is given (_Imports.razor injects
/// the Core services: here they throw if called) and the services a test adds. Errors that an error boundary catches
/// are recorded.
/// </summary>
internal static class AdminRenderer
{
    public static async Task<string> RenderAsync<TComponent>(Action<IServiceCollection> addServices,
        IDictionary<string, object?>? parameters = null, List<Exception>? caught = null)
        where TComponent : IComponent
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.Configure<CircuitOptions>(options => options.DetailedErrors = false);
        services.AddSingleton<IErrorBoundaryLogger>(new RecordingErrorBoundaryLogger(caught ?? []));
        services.AddSingleton<IJSRuntime, NoJsRuntime>();
        services.AddSingleton<NavigationManager, AdminNavigationManager>();
        AddImportedServices(services);

        addServices(services);

        await using var provider = services.BuildServiceProvider();
        await using var renderer = new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>());
        return await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var output = await renderer.RenderComponentAsync<TComponent>(
                ParameterView.FromDictionary(parameters ?? new Dictionary<string, object?>()));
            return output.ToHtmlString();
        });
    }

    /// <summary>The Core services _Imports.razor injects into every component of the package; here they throw if called.</summary>
    public static void AddImportedServices(IServiceCollection services)
    {
        var injected = new[]
        {
            typeof(IContentService), typeof(IMembershipService), typeof(IMediaService), typeof(ILanguageService),
            typeof(ITagService), typeof(ISeoService), typeof(IEmailService), typeof(IDataService)
        };
        foreach (var type in injected)
        {
            services.AddSingleton(type, AdminErrorBoundaryTests.Unused.Create(type));
        }
    }

    private sealed class RecordingErrorBoundaryLogger(List<Exception> caught) : IErrorBoundaryLogger
    {
        public ValueTask LogErrorAsync(Exception exception)
        {
            caught.Add(exception);
            return ValueTask.CompletedTask;
        }
    }

    private sealed class AdminNavigationManager : NavigationManager
    {
        public AdminNavigationManager() => Initialize("https://localhost/", "https://localhost/admin");
    }

    private sealed class NoJsRuntime : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => default;
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) => default;
    }
}

/// <summary>Records what a component logs.</summary>
internal sealed class RecordingLoggerProvider : ILoggerProvider
{
    public List<(LogLevel Level, string Message, Exception? Exception)> Entries { get; } = [];

    public ILogger CreateLogger(string categoryName) => new Logger(this);

    public void Dispose()
    {
    }

    private sealed class Logger(RecordingLoggerProvider owner) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            lock (owner.Entries)
            {
                owner.Entries.Add((logLevel, formatter(state, exception), exception));
            }
        }
    }
}
