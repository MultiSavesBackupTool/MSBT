using Avalonia.Controls.Notifications;
using Properties;

namespace Multi_Saves_Backup_Tool.Services;

public class NotificationService(INotificationManager notificationManager) : INotificationService
{
    public void ShowTaskRunning(string title, string? message = null)
    {
        message ??= Resources.Notification_TaskRunning;
        notificationManager.Show(new Notification(title, message));
    }

    public void ShowTaskCompleted(string title, string? message = null)
    {
        message ??= Resources.Notification_TaskCompleted;
        notificationManager.Show(new Notification(title, message, NotificationType.Success));
    }

    public void ShowTaskError(string title, string? message = null)
    {
        message ??= Resources.Notification_TaskError;
        notificationManager.Show(new Notification(title, message, NotificationType.Error));
    }
}