namespace ZauberCMS.Core.Shared;

public class AppState
{
    public event Func<Task>? OnContentTreeChanged;
    public event Func<Task>? OnContentChanged;

    public async Task NotifyContentTreeChanged() 
    {
        if (OnContentTreeChanged != null)
        {
            await OnContentTreeChanged.Invoke();
        }
    }

    public async Task NotifyContentChanged()
    {
        if (OnContentChanged != null)
        {
            await OnContentChanged.Invoke();
        }
    }
}