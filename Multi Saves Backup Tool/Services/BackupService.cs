using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Multi_Saves_Backup_Tool.Models;
using CompressionLevel = Multi_Saves_Backup_Tool.Models.CompressionLevel;

namespace Multi_Saves_Backup_Tool.Services;

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

        ZipArchive? archive = null;
        try
        {
            var settings = _settingsService.CurrentSettings.BackupSettings;
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            var safeName = GetSafeDirectoryName(game.GameName);
            var backupDir = Path.Combine(settings.BackupRootFolder, safeName);
            var archivePath = Path.Combine(backupDir, $"{timestamp}.zip");

            Directory.CreateDirectory(backupDir);

            _logger.LogInformation("Creating backup for game {GameName} at {Path}", game.GameName, archivePath);

            try
            {
                archive = ZipFile.Open(archivePath, ZipArchiveMode.Create);

                var backupSuccess = false;

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
                    archive.Dispose();
                    archive = null;
                    if (File.Exists(archivePath)) File.Delete(archivePath);
                }
                else
                {
                    _logger.LogInformation("Backup created successfully for {GameName}", game.GameName);
                }
            }
            catch (Exception)
            {
                archive?.Dispose();
                archive = null;

                await Task.Delay(100);

                if (File.Exists(archivePath))
                    try
                    {
                        File.Delete(archivePath);
                    }
                    catch (Exception deleteEx)
                    {
                        _logger.LogWarning(deleteEx, "Could not delete failed backup file {Path}", archivePath);
                    }

                throw;
            }
            finally
            {
                archive?.Dispose();
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

    public async Task ProcessSpecialBackup(GameModel game)
    {
        if (game == null)
            throw new ArgumentNullException(nameof(game));

        if (!game.SpecialBackupMark)
        {
            _logger.LogDebug("Special backup skipped for {GameName} - SpecialBackupMark is false", game.GameName);
            return;
        }

        try
        {
            _logger.LogInformation("Starting special backup process for {GameName}", game.GameName);

            if (!Directory.Exists(game.SavePath))
            {
                _logger.LogWarning("Source directory not found for game {GameName}: {Path}",
                    game.GameName, game.SavePath);
                return;
            }

            var settings = _settingsService.CurrentSettings.BackupSettings;
            var safeName = GetSafeDirectoryName(game.GameName);
            var archiveDir = Path.Combine(settings.BackupRootFolder, safeName, "SpecialArchive");

            Directory.CreateDirectory(archiveDir);

            var cutoffDate = DateTime.Now.AddDays(-1).ToString("yyMMdd");
            _logger.LogInformation("Processing special backup for {GameName} with cutoff date: {Date}",
                game.GameName, cutoffDate);

            var directories = Directory.GetDirectories(game.SavePath)
                .Select(dir => new
                {
                    Path = dir,
                    Name = Path.GetFileName(dir),
                    Date = ExtractDateFromDirectoryName(Path.GetFileName(dir))
                })
                .Where(d => !string.IsNullOrEmpty(d.Date))
                .ToList();

            if (!directories.Any())
            {
                _logger.LogInformation("No directories with date patterns found for {GameName}", game.GameName);
                return;
            }

            var uniqueDates = directories.Select(d => d.Date).Distinct().ToList();
            _logger.LogInformation("Found {Count} unique dates for {GameName}: {Dates}",
                uniqueDates.Count, game.GameName, string.Join(", ", uniqueDates));

            if (uniqueDates.Count <= 1)
            {
                _logger.LogInformation("All directories belong to single date for {GameName}. No archiving needed.",
                    game.GameName);
                return;
            }

            var archivedCount = 0;
            foreach (var dir in directories.Where(d =>
                         string.Compare(d.Date, cutoffDate, StringComparison.Ordinal) < 0))
                try
                {
                    var destinationPath = Path.Combine(archiveDir, dir.Name);

                    if (Directory.Exists(destinationPath))
                    {
                        var counter = 1;
                        var baseName = dir.Name;
                        while (Directory.Exists(destinationPath))
                        {
                            destinationPath = Path.Combine(archiveDir, $"{baseName}_({counter})");
                            counter++;
                        }
                    }

                    await CopyDirectoryAsync(dir.Path, destinationPath);
                    Directory.Delete(dir.Path, true);
                    _logger.LogInformation("Archived directory for {GameName}: {Source} -> {Destination}",
                        game.GameName, dir.Name, Path.GetFileName(destinationPath));
                    archivedCount++;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to archive directory {Directory} for {GameName}",
                        dir.Name, game.GameName);
                }

            _logger.LogInformation("Special backup completed for {GameName}. Archived {Count} directories",
                game.GameName, archivedCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Special backup failed for game {GameName}", game.GameName);
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

    private string? ExtractDateFromDirectoryName(string directoryName)
    {
        var patterns = new[]
        {
            @"[-_.](\d{6})[-_.]",
            @"^(\d{6})[-_.]",
            @"[-_.](\d{6})$",
            @"^(\d{6})$"
        };

        foreach (var pattern in patterns)
        {
            var match = Regex.Match(directoryName, pattern);
            if (match.Success)
            {
                var dateStr = match.Groups[1].Value;

                if (IsValidDate(dateStr)) return dateStr;
            }
        }

        return null;
    }

    private async Task CopyDirectoryAsync(string sourceDir, string destinationDir)
    {
        await Task.Run(() =>
        {
            Directory.CreateDirectory(destinationDir);

            foreach (var file in Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories))
            {
                var relativePath = Path.GetRelativePath(sourceDir, file);
                var destFile = Path.Combine(destinationDir, relativePath);

                Directory.CreateDirectory(Path.GetDirectoryName(destFile) ?? string.Empty);
                File.Copy(file, destFile, true);
            }
        });
    }

    private bool IsValidDate(string? dateStr)
    {
        if (dateStr != null && dateStr.Length != 6)
            return false;

        if (!int.TryParse(dateStr, out _))
            return false;

        try
        {
            var year = 2000 + int.Parse(dateStr.Substring(0, 2));
            var month = int.Parse(dateStr.Substring(2, 2));
            var day = int.Parse(dateStr.Substring(4, 2));

            if (year < 2020 || year > 2030) return false;
            if (month < 1 || month > 12) return false;
            if (day < 1 || day > 31) return false;

            var date = new DateTime(year, month, day);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private string GetSafeDirectoryName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars().Concat(Path.GetInvalidPathChars()).ToArray();
        return string.Join("_", name.Split(invalid));
    }

    private async Task AddToArchiveAsync(ZipArchive? archive, string sourcePath, string entryPrefix)
    {
        await Task.Run(() =>
        {
            var files = Directory.GetFiles(sourcePath, "*", SearchOption.AllDirectories);
            foreach (var file in files)
            {
                var relativePath = Path.GetRelativePath(sourcePath, file);
                var entryPath = Path.Combine(entryPrefix, relativePath);
                if (archive != null) archive.CreateEntryFromFile(file, entryPath, GetCompressionLevel());
            }
        });
    }

    private System.IO.Compression.CompressionLevel GetCompressionLevel()
    {
        return _settingsService.CurrentSettings.BackupSettings.CompressionLevel switch
        {
            CompressionLevel.Fastest => System.IO.Compression.CompressionLevel.Fastest,
            CompressionLevel.Optimal => System.IO.Compression.CompressionLevel.Optimal,
            CompressionLevel.SmallestSize => System.IO.Compression.CompressionLevel.NoCompression,
            _ => System.IO.Compression.CompressionLevel.Optimal
        };
    }
}