using ZauberCMS.Core.Content.Models;
using ZauberCMS.Core.Membership.Models;

namespace ZauberCMS.Core.Shared;

/// <summary>
/// Application-wide state manager using weak event subscriptions to prevent memory leaks.
/// Components can safely subscribe without worrying about disposal order.
/// </summary>
public class AppState
{
    // Weak event managers prevent memory leaks when Singleton AppState outlives Scoped components
    private readonly WeakEventManager<ContentType?> _contentTypeChanged = new();
    private readonly WeakEventManager<Content.Models.Content?> _contentChanged = new();
    private readonly WeakEventManager<User?> _userChanged = new();
    private readonly WeakEventManager<Media.Models.Media?> _mediaChanged = new();

    private readonly WeakEventManager<ContentType?> _contentTypeSaved = new();
    private readonly WeakEventManager<Content.Models.Content?> _contentSaved = new();
    private readonly WeakEventManager<User?> _userSaved = new();
    private readonly WeakEventManager<Media.Models.Media?> _mediaSaved = new();

    private readonly WeakEventManager<ContentType?> _contentTypeDeleted = new();
    private readonly WeakEventManager<Content.Models.Content?> _contentDeleted = new();
    private readonly WeakEventManager<User?> _userDeleted = new();
    private readonly WeakEventManager<Media.Models.Media?> _mediaDeleted = new();
    
    // Provide event-like interface for backward compatibility
    public event Func<ContentType?, string, Task>? OnContentTypeChanged
    {
        add => _contentTypeChanged.AddHandler(value!);
        remove => _contentTypeChanged.RemoveHandler(value!);
    }
    
    public event Func<Content.Models.Content?, string, Task>? OnContentChanged
    {
        add => _contentChanged.AddHandler(value!);
        remove => _contentChanged.RemoveHandler(value!);
    }
    
    public event Func<User?, string, Task>? OnUserChanged
    {
        add => _userChanged.AddHandler(value!);
        remove => _userChanged.RemoveHandler(value!);
    }
    
    public event Func<Media.Models.Media?, string, Task>? OnMediaChanged
    {
        add => _mediaChanged.AddHandler(value!);
        remove => _mediaChanged.RemoveHandler(value!);
    }
    
    public event Func<ContentType?, string, Task>? OnContentTypeSaved
    {
        add => _contentTypeSaved.AddHandler(value!);
        remove => _contentTypeSaved.RemoveHandler(value!);
    }
    
    public event Func<Content.Models.Content?, string, Task>? OnContentSaved
    {
        add => _contentSaved.AddHandler(value!);
        remove => _contentSaved.RemoveHandler(value!);
    }
    
    public event Func<User?, string, Task>? OnUserSaved
    {
        add => _userSaved.AddHandler(value!);
        remove => _userSaved.RemoveHandler(value!);
    }
    
    public event Func<Media.Models.Media?, string, Task>? OnMediaSaved
    {
        add => _mediaSaved.AddHandler(value!);
        remove => _mediaSaved.RemoveHandler(value!);
    }
    
    public event Func<ContentType?, string, Task>? OnContentTypeDeleted
    {
        add => _contentTypeDeleted.AddHandler(value!);
        remove => _contentTypeDeleted.RemoveHandler(value!);
    }
    
    public event Func<Content.Models.Content?, string, Task>? OnContentDeleted
    {
        add => _contentDeleted.AddHandler(value!);
        remove => _contentDeleted.RemoveHandler(value!);
    }
    
    public event Func<User?, string, Task>? OnUserDeleted
    {
        add => _userDeleted.AddHandler(value!);
        remove => _userDeleted.RemoveHandler(value!);
    }
    
    public event Func<Media.Models.Media?, string, Task>? OnMediaDeleted
    {
        add => _mediaDeleted.AddHandler(value!);
        remove => _mediaDeleted.RemoveHandler(value!);
    }
    
    public async Task NotifyMediaChanged(Media.Models.Media? media, string username) 
    {
        await _mediaChanged.RaiseEventAsync(media, username);
    }
    
    public async Task NotifyUserChanged(User? userObject, string username) 
    {
        await _userChanged.RaiseEventAsync(userObject, username);
    }
    
    public async Task NotifyContentTypeChanged(ContentType? contentType, string username) 
    {
        await _contentTypeChanged.RaiseEventAsync(contentType, username);
    }

    public async Task NotifyContentChanged(Content.Models.Content? content, string username)
    {
        await _contentChanged.RaiseEventAsync(content, username);
    }

    public async Task NotifyMediaSaved(Media.Models.Media? media, string username) 
    {
        await _mediaSaved.RaiseEventAsync(media, username);
        await NotifyMediaChanged(media, username);
    }
    
    public async Task NotifyUserSaved(User? user, string username) 
    {
        await _userSaved.RaiseEventAsync(user, username);
        await NotifyUserChanged(user, username);
    }
    
    public async Task NotifyContentTypeSaved(ContentType? contentType, string username) 
    {
        await _contentTypeSaved.RaiseEventAsync(contentType, username);
        await NotifyContentTypeChanged(contentType, username);
    }

    public async Task NotifyContentSaved(Content.Models.Content? content, string username)
    {
        await _contentSaved.RaiseEventAsync(content, username);
        await NotifyContentChanged(content, username);
    }

    public async Task NotifyMediaDeleted(Media.Models.Media? media, string username) 
    {
        await _mediaDeleted.RaiseEventAsync(media, username);
        await NotifyMediaChanged(media, username);
    }
    
    public async Task NotifyUserDeleted(User? userObject, string username) 
    {
        await _userDeleted.RaiseEventAsync(userObject, username);
        await NotifyUserChanged(userObject, username);
    }
    
    public async Task NotifyContentTypeDeleted(ContentType? contentType, string username) 
    {
        await _contentTypeDeleted.RaiseEventAsync(contentType, username);
        await NotifyContentTypeChanged(contentType, username);
    }

    public async Task NotifyContentDeleted(Content.Models.Content? content, string username)
    {
        await _contentDeleted.RaiseEventAsync(content, username);
        await NotifyContentChanged(content, username);
    }
}