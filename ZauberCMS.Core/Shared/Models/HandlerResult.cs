namespace ZauberCMS.Core.Shared.Models;

public class HandlerResult<T>
{
    public T Entity { get; set; } = default!;

    public bool Success { get; set; }

    public List<ResultMessage> Messages { get; set; } = new();

    public bool RefreshSignIn { get; set; }
}