namespace ZauberCMS.Web.Components.Layout;

public class SidebarState
{
    public bool IsExpanded { get; set; } = true;

    public event Action OnChange;

    public void Toggle()
    {
        IsExpanded = !IsExpanded;
        NotifyStateChanged();
    }

    private void NotifyStateChanged() => OnChange?.Invoke();
}