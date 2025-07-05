using System;
using System.IO;

namespace Multi_Saves_Backup_Tool.Models;

public class ServiceSettings
{
    public BackupSettings BackupSettings { get; set; } = new();
}

public class BackupSettings
{
    private int _maxParallelBackups = 2;

    private int _scanIntervalMinutes = 5;

    public string BackupRootFolder { get; set; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "MultiSavesBackupTool",
            "Backups");

    public int ScanIntervalMinutes
    {
        get => _scanIntervalMinutes;
        set
        {
            if (value < 1)
                throw new ArgumentException("Scan interval must be at least 1 minute");
            if (value > 1440)
                throw new ArgumentException("Scan interval cannot exceed 24 hours (1440 minutes)");
            _scanIntervalMinutes = value;
        }
    }

    public int MaxParallelBackups
    {
        get => _maxParallelBackups;
        set
        {
            if (value < 1)
                throw new ArgumentException("Max parallel backups must be at least 1");
            if (value > 10)
                throw new ArgumentException("Max parallel backups cannot exceed 10");
            _maxParallelBackups = value;
        }
    }

    public CompressionLevel CompressionLevel { get; set; } = CompressionLevel.Optimal;
    public string GamesConfigPath { get; set; } = "games.json";
    public bool EnableLogging { get; set; } = true;

    public TimeSpan GetScanInterval()
    {
        return TimeSpan.FromMinutes(ScanIntervalMinutes);
    }

    public string GetAbsolutePath(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
            throw new ArgumentException("Path cannot be null or empty", nameof(relativePath));

        if (Path.IsPathRooted(relativePath))
            return relativePath;
        return Path.GetFullPath(relativePath, AppContext.BaseDirectory);
    }

    public void ValidateBackupRootFolder()
    {
        if (string.IsNullOrWhiteSpace(BackupRootFolder))
            throw new InvalidOperationException("Backup root folder is not configured");

        try
        {
            var fullPath = Path.GetFullPath(BackupRootFolder);
            if (!Directory.Exists(fullPath)) Directory.CreateDirectory(fullPath);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Cannot create or access backup root folder: {ex.Message}", ex);
        }
    }
}

public enum CompressionLevel
{
    Optimal = 0,
    Fastest = 1,
    SmallestSize = 2
}