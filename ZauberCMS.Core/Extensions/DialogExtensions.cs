using Microsoft.AspNetCore.Components;
using Radzen;

namespace ZauberCMS.Core.Extensions;

public static class DialogExtensions
{
    public static async Task<object> OpenSidePanel<T>(this DialogService dialogService, string title, Dictionary<string, object>? parameters = null, string width = "1200px") where T : ComponentBase
    {
        return await dialogService.OpenSideAsync<T>(title, parameters, new SideDialogOptions { CloseDialogOnOverlayClick = true, Position = DialogPosition.Right, ShowMask = true, Width = width});
    }
}