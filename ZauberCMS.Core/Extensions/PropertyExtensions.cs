using System.Text.Json;
using ZauberCMS.Core.Content.Interfaces;

namespace ZauberCMS.Core.Extensions;

public static class PropertyExtensions
{
    public static async Task SavePropertySettings<T>(this IContentPropertySettings<T> cps, T settings)
    {
        var updatedSettingsModel = JsonSerializer.Serialize(settings);
        await cps.ValueChanged.InvokeAsync(updatedSettingsModel);
    }
    
    public static T GetPropertySettings<T>(this IContentPropertySettings<T> cps)
        where T : class, new()
    {
        if (!cps.SettingsModel.IsNullOrWhiteSpace())
        {
            return JsonSerializer.Deserialize<T>(cps.SettingsModel) ?? new T();
        }
        return new T();
    }
    
    public static T GetPropertySettings<T>(this IContentProperty cp)
        where T : class, new()
    {
        if (!cp.Settings.IsNullOrWhiteSpace())
        {
            return JsonSerializer.Deserialize<T>(cp.Settings) ?? new T();
        }
        return new T();
    }
    
}