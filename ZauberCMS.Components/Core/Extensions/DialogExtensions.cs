using Blazored.Modal;
using Blazored.Modal.Services;
using Microsoft.AspNetCore.Components;

namespace ZauberCMS.Core.Extensions;

public static class DialogExtensions
{
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