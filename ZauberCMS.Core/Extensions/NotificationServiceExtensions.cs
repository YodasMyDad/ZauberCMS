using Radzen;
using ZauberCMS.Core.Shared.Models;

namespace ZauberCMS.Core.Extensions;

public static class NotificationServiceExtensions
{
    public static void ShowErrorNotification(this NotificationService notificationService, string summary, string? detail = null, double? duration = 4000)
    {
        notificationService.ShowNotification(summary, NotificationSeverity.Error, detail, duration);
    }
    
    public static void ShowSuccessNotification(this NotificationService notificationService, string summary, string? detail = null, double? duration = 4000)
    {
        notificationService.ShowNotification(summary, NotificationSeverity.Success, detail, duration);
    }
    
    public static void ShowNotification(this NotificationService notificationService, string summary, NotificationSeverity severity, string? detail = null, double? duration = 4000)
    {
        notificationService.Notify(new NotificationMessage { Severity = severity, Summary = summary, Detail = detail, Duration = duration });
    }
    
    public static void ShowNotifications(this NotificationService notificationService, string summary, NotificationSeverity severity, List<string> details, double? duration = 4000)
    {
        foreach (var detail in details)
        {
            notificationService.Notify(new NotificationMessage { Severity = severity, Summary = summary, Detail = detail, Duration = duration });   
        }
    }
    
    public static void ShowNotifications(this NotificationService notificationService, List<ResultMessage> resultMessages, double? duration = 4000)
    {
        foreach (var message in resultMessages)
        {
            notificationService.Notify(new NotificationMessage { Severity = GetNotificationSeverity(message.MessageType), Summary = message.MessageType.ToString(), Detail = message.Message, Duration = duration });   
        }
    }
    
    private static NotificationSeverity GetNotificationSeverity(ResultMessageType messageType)
    {
        return messageType switch
        {
            ResultMessageType.Info => NotificationSeverity.Info,
            ResultMessageType.Success => NotificationSeverity.Success,
            ResultMessageType.Warning => NotificationSeverity.Warning,
            ResultMessageType.Error => NotificationSeverity.Error,
            _ => NotificationSeverity.Error
        };
    }
}