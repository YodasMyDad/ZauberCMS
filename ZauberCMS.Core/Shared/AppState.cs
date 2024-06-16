namespace ZauberCMS.Core.Shared;

public class AppState
{
    public event Func<Task>? OnContentTypeChanged;
    public event Func<Task>? OnContentChanged;
    public event Func<Task>? OnUsersChanged;
    public event Func<Task>? OnMediaChanged;

    public async Task NotifyMediaChanged() 
    {
        if (OnMediaChanged != null)
        {
            await OnMediaChanged.Invoke();
        }
    }
    
    public async Task NotifyUsersChanged() 
    {
        if (OnUsersChanged != null)
        {
            await OnUsersChanged.Invoke();
        }
    }
    
    public async Task NotifyContentTypeChanged() 
    {
        if (OnContentTypeChanged != null)
        {
            await OnContentTypeChanged.Invoke();
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