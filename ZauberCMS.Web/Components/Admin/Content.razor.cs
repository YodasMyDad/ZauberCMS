using Microsoft.AspNetCore.Components;
using ZauberCMS.Web.Components.Layout;

namespace ZauberCMS.Web.Components.Admin;

public partial class Content : ComponentBase
{
    [CascadingParameter]
    protected SidebarState? SidebarState { get; set; }
    
    public bool Expanded { get; set; } = true;

    public void ToggleSideBar()
    {
        Expanded = !Expanded;
    }
}