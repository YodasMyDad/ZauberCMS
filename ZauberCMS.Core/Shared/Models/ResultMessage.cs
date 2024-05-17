namespace ZauberCMS.Core.Shared.Models;

public class ResultMessage
{
    public string Message { get; set; } = string.Empty;
    public ResultMessageType MessageType { get; set; }
}