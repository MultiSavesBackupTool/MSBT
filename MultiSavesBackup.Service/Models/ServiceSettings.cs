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

    public TimeSpan GetScanInterval()
    {
        return TimeSpan.FromMinutes(ScanIntervalMinutes);
    }

    public string GetAbsolutePath(string relativePath)
    {
        if (Path.IsPathRooted(relativePath))
            return relativePath;
        return Path.GetFullPath(relativePath, AppContext.BaseDirectory);
    }

    public string GetAbsoluteGamesConfigPath()
    {
        return GetAbsolutePath(GamesConfigPath);
    }
}

public enum CompressionLevel
{
    Optimal = 0,
    Fastest = 1,
    SmallestSize = 2
}