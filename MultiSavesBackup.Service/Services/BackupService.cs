using System.IO.Compression;
using Multi_Saves_Backup_Tool.Models;
using CompressionLevel = System.IO.Compression.CompressionLevel;

namespace MultiSavesBackup.Service.Services;

public class BackupService : IBackupService
{
    private readonly ILogger<BackupService> _logger;
    private readonly ISettingsService _settingsService;

    public BackupService(ISettingsService settingsService, ILogger<BackupService> logger)
    {
        _settingsService = settingsService;
        _logger = logger;
    }

    public async Task CreateBackupAsync(GameModel game)
    {
        if (game == null)
            throw new ArgumentNullException(nameof(game));

        try
        {
            var settings = _settingsService.CurrentSettings.BackupSettings;
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            var safeName = GetSafeDirectoryName(game.GameName);
            var backupDir = Path.Combine(settings.BackupRootFolder, safeName);
            var archivePath = Path.Combine(backupDir, $"{timestamp}.zip");

            Directory.CreateDirectory(backupDir);

            _logger.LogInformation("Creating backup for game {GameName} at {Path}", game.GameName, archivePath);

            using var archive = ZipFile.Open(archivePath, ZipArchiveMode.Create);

            var backupSuccess = false;
            try
            {
                if (Directory.Exists(game.SavePath))
                {
                    await AddToArchiveAsync(archive, game.SavePath, "saves");
                    backupSuccess = true;
                }
                else
                {
                    _logger.LogWarning("Save path not found for game {GameName}: {Path}", game.GameName, game.SavePath);
                }

                if (!string.IsNullOrEmpty(game.ModPath) && Directory.Exists(game.ModPath))
                    await AddToArchiveAsync(archive, game.ModPath, "mods");

                if (!string.IsNullOrEmpty(game.AddPath) && Directory.Exists(game.AddPath))
                    await AddToArchiveAsync(archive, game.AddPath, "additional");

                if (!backupSuccess)
                {
                    _logger.LogWarning("No valid paths found for backup of game {GameName}", game.GameName);
                    if (File.Exists(archivePath)) File.Delete(archivePath);
                }
                else
                {
                    _logger.LogInformation("Backup created successfully for {GameName}", game.GameName);
                }
            }
            catch (Exception)
            {
                if (File.Exists(archivePath)) File.Delete(archivePath);
                throw;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create backup for game {GameName}", game.GameName);
            throw;
        }
    }

    public void CleanupOldBackups(GameModel game)
    {
        if (game == null)
            throw new ArgumentNullException(nameof(game));

        try
        {
            if (game.DaysForKeep <= 0)
            {
                _logger.LogInformation("Cleanup skipped for {GameName} as DaysForKeep is {Days}",
                    game.GameName, game.DaysForKeep);
                return;
            }

            var safeName = GetSafeDirectoryName(game.GameName);
            var backupDir = Path.Combine(_settingsService.CurrentSettings.BackupSettings.BackupRootFolder, safeName);
            if (!Directory.Exists(backupDir))
            {
                _logger.LogInformation("No backup directory found for {GameName}", game.GameName);
                return;
            }

            var cutoffDate = DateTime.Now.AddDays(-game.DaysForKeep);
            var files = Directory.GetFiles(backupDir, "*.zip")
                .Select(f => new FileInfo(f))
                .Where(f => f.CreationTime < cutoffDate)
                .ToList();

            if (!files.Any())
            {
                _logger.LogInformation("No old backups found for {GameName}", game.GameName);
                return;
            }

            _logger.LogInformation("Found {Count} old backups to delete for {GameName}", files.Count, game.GameName);

            foreach (var file in files)
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
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to cleanup old backups for game {GameName}", game.GameName);
            throw;
        }
    }

    public bool VerifyBackupPaths(GameModel game)
    {
        if (game == null)
            throw new ArgumentNullException(nameof(game));

        var hasValidPath = false;

        if (!Directory.Exists(game.SavePath))
            _logger.LogWarning("Save path not found for game {GameName}: {Path}",
                game.GameName, game.SavePath);
        else
            hasValidPath = true;

        if (!string.IsNullOrEmpty(game.ModPath) && !Directory.Exists(game.ModPath))
            _logger.LogWarning("Mod path not found for game {GameName}: {Path}",
                game.GameName, game.ModPath);
        else if (!string.IsNullOrEmpty(game.ModPath)) hasValidPath = true;

        if (!string.IsNullOrEmpty(game.AddPath) && !Directory.Exists(game.AddPath))
            _logger.LogWarning("Additional path not found for game {GameName}: {Path}",
                game.GameName, game.AddPath);
        else if (!string.IsNullOrEmpty(game.AddPath)) hasValidPath = true;

        return hasValidPath;
    }

    private string GetSafeDirectoryName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars().Concat(Path.GetInvalidPathChars()).ToArray();
        return string.Join("_", name.Split(invalid));
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

    private CompressionLevel GetCompressionLevel()
    {
        var compressionLevel =
            (Multi_Saves_Backup_Tool.Models.CompressionLevel)_settingsService.CurrentSettings.BackupSettings
                .CompressionLevel;
        return compressionLevel switch
        {
            Multi_Saves_Backup_Tool.Models.CompressionLevel.Fastest => CompressionLevel
                .NoCompression,
            Multi_Saves_Backup_Tool.Models.CompressionLevel.SmallestSize => CompressionLevel
                .Optimal,
            _ => CompressionLevel.NoCompression
        };
    }
}