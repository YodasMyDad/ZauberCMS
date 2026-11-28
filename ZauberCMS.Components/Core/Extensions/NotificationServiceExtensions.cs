using Microsoft.Extensions.Localization;
using Radzen;
using ZauberCMS.Core.Shared.Models;

namespace ZauberCMS.Core.Extensions;

public static class NotificationServiceExtensions
{
    public static void ShowErrorNotification(this NotificationService notificationService, string summary, string? detail = null, double? duration = 4000)
    {
        notificationService.ShowNotification(summary, NotificationSeverity.Error, detail, duration);
    }
    
    public static void ShowWarningNotification(this NotificationService notificationService, string summary, string? detail = null, double? duration = 4000)
    {
        notificationService.ShowNotification(summary, NotificationSeverity.Warning, detail, duration);
    }
    
    public static void ShowSuccessNotification(this NotificationService notificationService, string summary, string? detail = null, double? duration = 4000)
    {
        notificationService.ShowNotification(summary, NotificationSeverity.Success, detail, duration);
    }
    
    public static void ShowNotification(this NotificationService notificationService, string summary, NotificationSeverity severity, string? detail = null, double? duration = 4000)
    {
        notificationService.Notify(new NotificationMessage { Severity = severity, Summary = summary, Detail = detail ?? string.Empty, Duration = duration });
    }
    
    public static void ShowNotifications(this NotificationService notificationService, string summary, NotificationSeverity severity, List<string> details, double? duration = 4000)
    {
        foreach (var detail in details)
        {
            notificationService.Notify(new NotificationMessage { Severity = severity, Summary = summary, Detail = detail, Duration = duration });   
        }
    }

    /// <summary>
    /// One notification per message, its summary the English name of its type ("Success", "Error"). The admin passes
    /// its text, <see cref="ShowNotifications(NotificationService, List{ResultMessage}, IStringLocalizer, double?)"/>,
    /// so the summary is in the admin's language.
    /// </summary>
    public static void ShowNotifications(this NotificationService notificationService, List<ResultMessage> resultMessages, double? duration = 4000)
    {
        foreach (var message in resultMessages)
        {
            notificationService.Notify(new NotificationMessage { Severity = GetNotificationSeverity(message.MessageType), Summary = message.MessageType.ToString(), Detail = message.Message, Duration = duration });   
        }
    }

    /// <summary>
    /// One notification per message, its summary the type of the message in the current language
    /// (<c>general_success</c>, <c>general_error</c>, <c>general_warning</c>, <c>general_info</c>; Roadmap D37).
    /// The message itself is shown as the service wrote it.
    /// </summary>
    /// <param name="text">ZauberCMS's text: a component's <c>L</c>, <c>IStringLocalizer&lt;ZauberText&gt;</c>.</param>
    public static void ShowNotifications(this NotificationService notificationService, List<ResultMessage> resultMessages, IStringLocalizer text, double? duration = 4000)
    {
        foreach (var message in resultMessages)
        {
            notificationService.Notify(new NotificationMessage { Severity = GetNotificationSeverity(message.MessageType), Summary = Summary(message.MessageType, text), Detail = message.Message, Duration = duration });
        }
    }

    // The keys are literals so the text guard test sees them
    private static string Summary(ResultMessageType messageType, IStringLocalizer text) => messageType switch
    {
        ResultMessageType.Info => text["general_info"],
        ResultMessageType.Success => text["general_success"],
        ResultMessageType.Warning => text["general_warning"],
        _ => text["general_error"]
    };
    
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
