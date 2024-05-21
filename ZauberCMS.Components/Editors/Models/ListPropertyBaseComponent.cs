using System.Text.Json;
using Microsoft.AspNetCore.Components;
using ZauberCMS.Core.Content.Interfaces;
using ZauberCMS.Core.Extensions;
using ZauberCMS.Core.Plugins;

namespace ZauberCMS.Components.Editors.Models;

public class ListPropertyBaseComponent : ComponentBase
{
    [Inject] public ExtensionManager ExtensionManager { get; set; } = default!;

    [Parameter] public string Value { get; set; } = string.Empty;
    [Parameter] public EventCallback<string> ValueChanged { get; set; }
    [Parameter] public string Settings { get; set; } = string.Empty;
    protected IEnumerable<string> SelectedValues { get; set; } = Enumerable.Empty<string>();
    protected string SelectedValue { get; set; } = string.Empty;
    protected ListPropertySettingsModel SettingsModel { get; set; } = new();

    protected override void OnInitialized()
    {
        base.OnInitialized();

        if (!Value.IsNullOrWhiteSpace())
        {
            //TODO - Must be a better way to do this :/
            try
            {
                SelectedValues = JsonSerializer.Deserialize<IEnumerable<string>>(Value) ?? Enumerable.Empty<string>();
            }
            catch (JsonException) // If exception is thrown then it is not valid JSON
            {
                SelectedValue = Value; // It's not JSON, so treat as SelectedValue
            }
        }

        if (!Settings.IsNullOrWhiteSpace())
        {
            SettingsModel = JsonSerializer.Deserialize<ListPropertySettingsModel>(Settings) ??
                            new ListPropertySettingsModel();

            if (!SettingsModel.DataSource.IsNullOrWhiteSpace())
            {
                // We have a datasource
                var allDataListSources = ExtensionManager.GetInstances<IDataListSource>();
                if (allDataListSources.TryGetValue(SettingsModel.DataSource, out var listSource))
                {
                    SettingsModel.Items = listSource.GetItems().ToList();
                }
            }
        }
    }

    protected async Task OnChange(string args)
    {
        SelectedValue = args;
        Value = args;
        await ValueChanged.InvokeAsync(Value);
    }

    protected async Task OnChange()
    {
        await OnChange(SelectedValues);
    }

    protected async Task OnChange(IEnumerable<string> args)
    {
        SelectedValues = args;
        Value = JsonSerializer.Serialize(SelectedValues);
        await ValueChanged.InvokeAsync(Value);
    }
}