using Blazored.Modal;
using Blazored.Modal.Services;
using Microsoft.AspNetCore.Components;
using Radzen;

namespace ZauberCMS.Core.Extensions;

public static class DialogExtensions
{
    public static async Task<object> OpenSidePanel<T>(this DialogService dialogService, string title, Dictionary<string, object>? parameters = null, string width = "1200px") where T : ComponentBase
    {
        return await dialogService.OpenSideAsync<T>(title, parameters, new SideDialogOptions { CloseDialogOnOverlayClick = true, Position = DialogPosition.Right, ShowMask = true, Width = width});
    }
    
    public static IModalReference OpenSidePanel<T>(this IModalService dialogService, string title, Dictionary<string, object>? parameters = null) where T : ComponentBase
    {
        var modalParameters = new ModalParameters();
        if (parameters != null)
        {
            foreach (var p in parameters)
            {
                modalParameters.Add(p.Key, p.Value);
            }
            
        }
        var options = new ModalOptions
        { 
            Size = ModalSize.Large,
            Position = ModalPosition.Custom,
            PositionCustomClass = "side-panel"
        };
        return dialogService.Show<T>(title, modalParameters, options);
    }
}