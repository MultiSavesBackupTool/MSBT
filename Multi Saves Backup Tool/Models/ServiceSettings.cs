namespace Multi_Saves_Backup_Tool.Models;

public class ServiceSettings
{
    public BackupSettings BackupSettings { get; set; } = new();
}

public class BackupSettings
{
    public string BackupRootFolder { get; set; } = "C:\\Backups";
    public int ScanIntervalMinutes { get; set; } = 5;
    public int MaxParallelBackups { get; set; } = 2;
    public CompressionLevel CompressionLevel { get; set; } = CompressionLevel.Optimal;
    public string GamesConfigPath { get; set; } = "games.json";

    public bool EnableLogging { get; set; } = true;
    //public NotificationSettings NotificationSettings { get; set; } = new();
}

//public class NotificationSettings
//{
//    public bool EnableNotifications { get; set; } = true;
//    public bool ShowErrorNotifications { get; set; } = true;
//    public bool ShowSuccessNotifications { get; set; } = true;
//}

public enum CompressionLevel
{
    Fastest = 0,
    Optimal = 1,
    SmallestSize = 2
}