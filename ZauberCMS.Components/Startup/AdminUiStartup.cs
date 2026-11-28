using Blazored.Modal;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Radzen;
using ZauberCMS.Core.Media.Services;
using ZauberCMS.Core.Plugins.Interfaces;
using ZauberCMS.Core.Shared.Services;

namespace ZauberCMS.Components.Startup;

/// <summary>
/// Registers the admin's UI services: Radzen's (dialogs, notifications, context menus, tooltips), the vendored modal
/// and the UI-side helpers that show notifications. They were registered by Core's <c>AddZauberCms</c> until the UI
/// moved out of Core (ZA-091, Roadmap D51); the bootstrap finds this class in ZauberCMS.Components as it finds any
/// start-up plugin, so a site calls nothing new.
/// </summary>
internal sealed class AdminUiStartup : IStartupPlugin
{
    public void Register(IServiceCollection services, IConfiguration configuration)
    {
        services.AddRadzenComponents();
        // Radzen's own text through ZauberCMS's (Roadmap D93): a site that registers its own ILocalizer keeps it
        services.TryAddSingleton<ILocalizer, RadzenTextLocalizer>();
        services.AddBlazoredModal();
        services.AddScoped(typeof(ValidateService<>));
        services.AddScoped<MediaValidationService>();
    }
}
