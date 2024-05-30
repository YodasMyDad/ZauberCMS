namespace ZauberCMS.Core.Shared;

public class AppState
{
    public event Func<Task>? OnContentTypeChanged;
    public event Func<Task>? OnContentChanged;
    public event Func<Task>? OnMembersChanged;

    public async Task NotifyMembersChanged() 
    {
        if (OnMembersChanged != null)
        {
            await OnMembersChanged.Invoke();
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