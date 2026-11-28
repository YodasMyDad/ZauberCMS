using System.Text.Json;
using Blazored.Modal.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Radzen;
using ZauberCMS.Core.Content.Interfaces;
using ZauberCMS.Core.Content.Models;
using ZauberCMS.Core.Extensions;
using ZauberCMS.Core.Localization;
using ZauberCMS.Core.Plugins;

namespace ZauberCMS.Components.Editors.Models;

public class ListPropertyBaseComponent : ComponentBase
{
    [Inject] public ExtensionManager ExtensionManager { get; set; } = null!;
    [Inject] public NotificationService NotificationService { get; set; } = null!;
    [Inject] public IServiceProvider ServiceProvider { get; set; } = null!;

    /// <summary>ZauberCMS's text (Docs/AdminUI.md "Localisation"); the Razor editors built on this class also have <c>L</c>.</summary>
    [Inject] private IStringLocalizer<ZauberText> Text { get; set; } = null!;

    [Parameter] public string? Value { get; set; } = string.Empty;
    [Parameter] public EventCallback<string> ValueChanged { get; set; }
    [Parameter] public string? Settings { get; set; } = string.Empty;
    [Parameter] public Content? Content { get; set; }
    [Parameter] public string? PropertyAlias { get; set; }
    [CascadingParameter] public IModalService? ModalService { get; set; }

    protected IEnumerable<string> SelectedValues { get; set; } = [];
    protected string SelectedValue { get; set; } = string.Empty;
    protected ListPropertySettingsModel SettingsModel { get; set; } = new();

    protected override async Task OnInitializedAsync()
    {
        if (!Value.IsNullOrWhiteSpace())
        {
            //TODO - Must be a better way to do this :/
            try
            {
                SelectedValues = JsonSerializer.Deserialize<IEnumerable<string>>(Value) ?? [];
            }
            catch (JsonException) // If exception is thrown then it is not valid JSON
            {
                SelectedValue = Value; // It's not JSON, so treat as SelectedValue
            }
        }

        SettingsModel = Settings.FromJson<ListPropertySettingsModel>();

        if (!SettingsModel.DataSource.IsNullOrWhiteSpace())
        {
            // We have a datasource
            var allDataListSources = ExtensionManager.GetInstances<IDataListSource>();
            if (allDataListSources.TryGetValue(SettingsModel.DataSource, out var listSource))
            {
                using var scope = ServiceProvider.CreateScope();
                // Async: a source that queries the database must not block the circuit (ZA-054)
                SettingsModel.Items = (await listSource.GetItemsAsync(scope, Content)).ToList();
            }
        }
    }

    protected async Task OnChange(string args)
    {
        SelectedValue = args;
        Value = args;
        await ValueChanged.InvokeAsync(Value);
    }

    protected async Task OnChange(bool restrictMax)
    {
        await OnChange(SelectedValues, restrictMax);
    }

    protected async Task OnChange(IEnumerable<string> args, bool restrictMax)
    {
        var selectedValues = args.ToList();
        if (restrictMax && SettingsModel.MaxAmount > 0 && selectedValues.Count > SettingsModel.MaxAmount)
        {
            NotificationService.Notify(new NotificationMessage
            {
                Severity = NotificationSeverity.Error,
                Summary = Text["general_error"],
                Detail = Text["editors_selectMaxAmount", SettingsModel.MaxAmount],
                Duration = 4000
            });

            // Remove excess items
            SelectedValues = selectedValues.Take(SettingsModel.MaxAmount).ToList();
        }
        else
        {
            SelectedValues = selectedValues;
        }

        Value = JsonSerializer.Serialize(SelectedValues);
        await ValueChanged.InvokeAsync(Value);
    }
}