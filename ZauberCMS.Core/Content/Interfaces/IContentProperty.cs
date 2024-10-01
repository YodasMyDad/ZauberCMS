using Blazored.Modal.Services;
using Microsoft.AspNetCore.Components;

namespace ZauberCMS.Core.Content.Interfaces;

public interface IContentProperty
{
    string Name { get; }
    string Alias { get; }
    string Description { get;}
    string Icon { get; }
    public Type? SettingsComponent { get; set; }

    string? Value { get; set; }
    EventCallback<string> ValueChanged { get; set; }
    string? Settings { get; set; }
    Models.Content? Content { get; set; }
    IModalService? ModalService { get; set; }
    List<string> Scripts { get; set; }
    List<string> Styles { get; set; }
}