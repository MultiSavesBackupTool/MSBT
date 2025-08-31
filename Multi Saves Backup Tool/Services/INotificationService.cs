namespace Multi_Saves_Backup_Tool.Services;

public interface INotificationService
{
    void ShowTaskRunning(string title, string? message = null);
    void ShowTaskCompleted(string title, string? message = null);
    void ShowTaskError(string title, string? message = null);
}