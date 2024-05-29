namespace ZauberCMS.Core.Shared;

public class AppState
{
    public event Action? OnContentChanged;
    public event Action? OnContentTypeChanged;

    public void NotifyContentChanged() => OnContentChanged?.Invoke();
    public void NotifyContentTypeChanged() => OnContentTypeChanged?.Invoke();
}