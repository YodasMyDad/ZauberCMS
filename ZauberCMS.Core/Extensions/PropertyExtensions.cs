using System.Text.Json;
using ZauberCMS.Core.Content.Interfaces;

namespace ZauberCMS.Core.Extensions;

public static class PropertyExtensions
{
    public static async Task SavePropertySettings<T>(this IContentPropertySettings cps, T settings)
    {
        var updatedSettingsModel = JsonSerializer.Serialize(settings);
        await cps.ValueChanged.InvokeAsync(updatedSettingsModel);
    }
    
    public static T? GetPropertySettings<T>(this IContentPropertySettings cps)
    {
        if (!cps.SettingsModel.IsNullOrWhiteSpace())
        {
            return JsonSerializer.Deserialize<T>(cps.SettingsModel) ?? default(T);
        }
        return default;
    }
}