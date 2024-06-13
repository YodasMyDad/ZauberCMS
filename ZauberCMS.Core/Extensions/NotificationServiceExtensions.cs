using Radzen;

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
}