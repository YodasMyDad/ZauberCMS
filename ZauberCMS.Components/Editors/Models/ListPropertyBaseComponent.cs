using System.Text.Json;
using Microsoft.AspNetCore.Components;
using ZauberCMS.Core.Content.Interfaces;
using ZauberCMS.Core.Content.Models;
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
    protected ListPropertySettingsModel SettingsModel { get; set; } = new();
    
    protected override void OnInitialized()
    {
        base.OnInitialized();

        if (!Value.IsNullOrWhiteSpace())
        {
            SelectedValues = JsonSerializer.Deserialize<IEnumerable<string>>(Value) ?? Enumerable.Empty<string>();
        }
        
        if (!Settings.IsNullOrWhiteSpace())
        {
            SettingsModel = JsonSerializer.Deserialize<ListPropertySettingsModel>(Settings) ?? new ListPropertySettingsModel();

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

    protected async Task OnChange(IEnumerable<string> args)
    {
        SelectedValues = args;
        Value = JsonSerializer.Serialize(SelectedValues);
        await ValueChanged.InvokeAsync(Value);
    }
}