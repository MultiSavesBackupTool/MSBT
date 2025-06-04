using Multi_Saves_Backup_Tool.Models;
using MultiSavesBackup.Service.Models;
using System.IO.Compression;
using Microsoft.Extensions.Logging;

namespace MultiSavesBackup.Service.Services;

public class BackupService : IBackupService
{
    private readonly ISettingsService _settingsService;
    private readonly ILogger<BackupService> _logger;

    public BackupService(ISettingsService settingsService, ILogger<BackupService> logger)
    {
        _settingsService = settingsService;
        _logger = logger;
    }

    public async Task CreateBackupAsync(GameModel game)
    {
        try
        {
            var settings = _settingsService.CurrentSettings.BackupSettings;
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            var backupDir = Path.Combine(settings.BackupRootFolder, game.GameName);
            var archivePath = Path.Combine(backupDir, $"{timestamp}.zip");

            Directory.CreateDirectory(backupDir);

            _logger.LogInformation("Creating backup for game {GameName} at {Path}", game.GameName, archivePath);

            using var archive = ZipFile.Open(archivePath, ZipArchiveMode.Create);
            
            if (Directory.Exists(game.SavePath))
            {
                await AddToArchiveAsync(archive, game.SavePath, "saves");
            }

            if (!string.IsNullOrEmpty(game.ModPath) && Directory.Exists(game.ModPath))
            {
                await AddToArchiveAsync(archive, game.ModPath, "mods");
            }

            if (!string.IsNullOrEmpty(game.AddPath) && Directory.Exists(game.AddPath))
            {
                await AddToArchiveAsync(archive, game.AddPath, "additional");
            }

            _logger.LogInformation("Backup created successfully for {GameName}", game.GameName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create backup for game {GameName}", game.GameName);
            throw;
        }
    }

    public void CleanupOldBackups(GameModel game)
    {
        try
        {
            if (game.DaysForKeep <= 0)
            {
                _logger.LogInformation("Cleanup skipped for {GameName} as DaysForKeep is {Days}", 
                    game.GameName, game.DaysForKeep);
                return;
            }

            var backupDir = Path.Combine(_settingsService.CurrentSettings.BackupSettings.BackupRootFolder, game.GameName);
            if (!Directory.Exists(backupDir))
                return;

            var cutoffDate = DateTime.Now.AddDays(-game.DaysForKeep);
            var files = Directory.GetFiles(backupDir, "*.zip")
                               .Select(f => new FileInfo(f))
                               .Where(f => f.CreationTime < cutoffDate)
                               .ToList();

            foreach (var file in files)
            {
                try
                {
                    file.Delete();
                    _logger.LogInformation("Deleted old backup {FileName} for {GameName}", 
                        file.Name, game.GameName);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to delete old backup {FileName} for {GameName}", 
                        file.Name, game.GameName);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to cleanup old backups for game {GameName}", game.GameName);
            throw;
        }
    }

    public bool VerifyBackupPaths(GameModel game)
    {
        if (!Directory.Exists(game.SavePath))
        {
            _logger.LogWarning("Save path not found for game {GameName}: {Path}", 
                game.GameName, game.SavePath);
            return false;
        }

        if (!string.IsNullOrEmpty(game.ModPath) && !Directory.Exists(game.ModPath))
        {
            _logger.LogWarning("Mod path not found for game {GameName}: {Path}", 
                game.GameName, game.ModPath);
            return false;
        }

        if (!string.IsNullOrEmpty(game.AddPath) && !Directory.Exists(game.AddPath))
        {
            _logger.LogWarning("Additional path not found for game {GameName}: {Path}", 
                game.GameName, game.AddPath);
            return false;
        }

        return true;
    }

    private async Task AddToArchiveAsync(ZipArchive archive, string sourcePath, string entryPrefix)
    {
        await Task.Run(() =>
        {
            var files = Directory.GetFiles(sourcePath, "*", SearchOption.AllDirectories);
            foreach (var file in files)
            {
                var relativePath = Path.GetRelativePath(sourcePath, file);
                var entryPath = Path.Combine(entryPrefix, relativePath);
                archive.CreateEntryFromFile(file, entryPath, GetCompressionLevel());
            }
        });
    }

    private System.IO.Compression.CompressionLevel GetCompressionLevel()
    {
        var compressionLevel = (Multi_Saves_Backup_Tool.Models.CompressionLevel)_settingsService.CurrentSettings.BackupSettings.CompressionLevel;
        return compressionLevel switch
        {
            Multi_Saves_Backup_Tool.Models.CompressionLevel.Fastest => System.IO.Compression.CompressionLevel.NoCompression,
            Multi_Saves_Backup_Tool.Models.CompressionLevel.SmallestSize => System.IO.Compression.CompressionLevel.Optimal,
            _ => System.IO.Compression.CompressionLevel.NoCompression
        };
    }
}
