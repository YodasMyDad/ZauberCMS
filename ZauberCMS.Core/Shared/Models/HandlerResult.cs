namespace ZauberCMS.Core.Shared.Models;

public class HandlerResult<T>
{
    public T? Entity { get; set; }

    public bool Success { get; set; }

    public List<ResultMessage> Messages { get; set; } = new();
    
    /// <summary>
    /// The signed-in user changed their own account, so their sign-in cookie must be re-issued
    /// (navigate to <c>Urls.ApiRefreshSignIn</c> with <see cref="RefreshSignInTicket"/>).
    /// </summary>
    public bool RefreshSignIn { get; set; }

    /// <summary>
    /// Short-lived ticket for the refresh endpoint, set together with <see cref="RefreshSignIn"/>.
    /// Pass it as the <c>ticket</c> query value.
    /// </summary>
    public string? RefreshSignInTicket { get; set; }
}