using Microsoft.AspNetCore.Components;

namespace ZauberCMS.Core.Content.Interfaces;

public interface IContentPropertySettings<T>
{
    EventCallback<string> ValueChanged { get; set; }
    public string? SettingsModel { get; set; }
    public T Settings { get; set; }
}