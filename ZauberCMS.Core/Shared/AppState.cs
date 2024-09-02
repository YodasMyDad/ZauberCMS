using ZauberCMS.Core.Content.Models;
using ZauberCMS.Core.Membership.Models;

namespace ZauberCMS.Core.Shared;

public class AppState
{
    public event Func<ContentType?, string, Task>? OnContentTypeChanged;
    public event Func<Content.Models.Content?, string, Task>? OnContentChanged;
    public event Func<User?, string, Task>? OnUserChanged;
    public event Func<Media.Models.Media?, string, Task>? OnMediaChanged;

    public event Func<ContentType?, string, Task>? OnContentTypeSaved;
    public event Func<Content.Models.Content?, string, Task>? OnContentSaved;
    public event Func<User?, string, Task>? OnUserSaved;
    public event Func<Media.Models.Media?, string, Task>? OnMediaSaved;

    public event Func<ContentType?, string, Task>? OnContentTypeDeleted;
    public event Func<Content.Models.Content?, string, Task>? OnContentDeleted;
    public event Func<User?, string, Task>? OnUserDeleted;
    public event Func<Media.Models.Media?, string, Task>? OnMediaDeleted;
    
    public async Task NotifyMediaChanged(Media.Models.Media? media, string username) 
    {
        if (OnMediaChanged != null)
        {
            await OnMediaChanged.Invoke(media, username);
        }
    }
    
    public async Task NotifyUserChanged(User? userObject, string username) 
    {
        if (OnUserChanged != null)
        {
            await OnUserChanged.Invoke(userObject, username);
        }
    }
    
    public async Task NotifyContentTypeChanged(ContentType? contentType, string username) 
    {
        if (OnContentTypeChanged != null)
        {
            await OnContentTypeChanged.Invoke(contentType, username);
        }
    }

    public async Task NotifyContentChanged(Content.Models.Content? content, string username)
    {
        if (OnContentChanged != null)
        {
            await OnContentChanged.Invoke(content, username);
        }
    }

    public async Task NotifyMediaSaved(Media.Models.Media? media, string username) 
    {
        if (OnMediaSaved != null)
        {
            await OnMediaSaved.Invoke(media, username);
        }
        await NotifyMediaChanged(media, username);
    }
    
    public async Task NotifyUserSaved(User? user, string username) 
    {
        if (OnUserSaved != null)
        {
            await OnUserSaved.Invoke(user, username);
        }
        await NotifyUserChanged(user, username);
    }
    
    public async Task NotifyContentTypeSaved(ContentType? contentType, string username) 
    {
        if (OnContentTypeSaved != null)
        {
            await OnContentTypeSaved.Invoke(contentType, username);
        }
        await NotifyContentTypeChanged(contentType, username); // Trigger changed event
    }

    public async Task NotifyContentSaved(Content.Models.Content? content, string username)
    {
        if (OnContentSaved != null)
        {
            await OnContentSaved.Invoke(content, username);
        }
        await NotifyContentChanged(content, username); // Trigger changed event
    }

    public async Task NotifyMediaDeleted(Media.Models.Media? media, string username) 
    {
        if (OnMediaDeleted != null)
        {
            await OnMediaDeleted.Invoke(media, username);
        }
        await NotifyMediaChanged(media, username); // Trigger changed event
    }
    
    public async Task NotifyUserDeleted(User? userObject, string username) 
    {
        if (OnUserDeleted != null)
        {
            await OnUserDeleted.Invoke(userObject, username);
        }
        await NotifyUserChanged(userObject, username); // Trigger changed event
    }
    
    public async Task NotifyContentTypeDeleted(ContentType? contentType, string username) 
    {
        if (OnContentTypeDeleted != null)
        {
            await OnContentTypeDeleted.Invoke(contentType, username);
        }
        await NotifyContentTypeChanged(contentType, username); // Trigger changed event
    }

    public async Task NotifyContentDeleted(Content.Models.Content? content, string username)
    {
        if (OnContentDeleted != null)
        {
            await OnContentDeleted.Invoke(content, username);
        }
        await NotifyContentChanged(content, username); // Trigger changed event
    }
}