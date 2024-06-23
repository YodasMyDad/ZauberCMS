namespace ZauberCMS.Core.Shared.Models;

public class ResultMessage
{
    public ResultMessage()
    {
        
    }

    public ResultMessage(string message, ResultMessageType messageType)
    {
        Message = message;
        MessageType = messageType;
    }
    
    public string Message { get; set; } = string.Empty;
    public ResultMessageType MessageType { get; set; }
}