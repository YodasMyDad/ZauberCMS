using Microsoft.AspNetCore.Components;

namespace ZauberCMS.Core.Content.Interfaces;

public interface IContentPropertySettings
{
    EventCallback<string> ValueChanged { get; set; }
    public string? SettingsModel { get; set; }
}