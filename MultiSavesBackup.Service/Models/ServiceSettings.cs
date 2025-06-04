namespace MultiSavesBackup.Service.Models;

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
    public string LogPath { get; set; } = "backup_service.log";
    public NotificationSettings NotificationSettings { get; set; } = new();

    public TimeSpan GetScanInterval() => TimeSpan.FromMinutes(ScanIntervalMinutes);
    
    public string GetAbsolutePath(string relativePath)
    {
        if (Path.IsPathRooted(relativePath))
            return relativePath;
        return Path.GetFullPath(relativePath, AppContext.BaseDirectory);
    }

    public string GetAbsoluteGamesConfigPath() => GetAbsolutePath(GamesConfigPath);
    public string GetAbsoluteLogPath() => GetAbsolutePath(LogPath);
}

public class NotificationSettings
{
    public bool EnableNotifications { get; set; } = true;
    public bool ShowErrorNotifications { get; set; } = true;
    public bool ShowSuccessNotifications { get; set; } = true;
}

public enum CompressionLevel
{
    Optimal,
    Fastest,
    NoCompression
}

