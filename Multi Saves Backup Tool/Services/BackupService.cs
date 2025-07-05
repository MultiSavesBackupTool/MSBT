using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Multi_Saves_Backup_Tool.Models;
using SharpCompress.Archives.Zip;
using SharpCompress.Common;
using SharpCompress.Writers;
using CompressionLevel = Multi_Saves_Backup_Tool.Models.CompressionLevel;

namespace Multi_Saves_Backup_Tool.Services;

public class BackupService(ISettingsService settingsService, ILogger<BackupService> logger)
    : IBackupService
{
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private readonly ILogger<BackupService> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    private readonly ISettingsService _settingsService =
        settingsService ?? throw new ArgumentNullException(nameof(settingsService));

    private bool _disposed;

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    public async Task CreateBackupAsync(GameModel? game, bool isPermanent)
    {
        ThrowIfDisposed();

        if (game == null)
            throw new ArgumentNullException(nameof(game));

        ZipArchive? archive = null;
        List<FileStream> streams = new();

        try
        {
            var settings = _settingsService.CurrentSettings.BackupSettings;
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            var safeName = GetSafeDirectoryName(game.GameName);
            var backupDir = Path.Combine(settings.BackupRootFolder, safeName);
            if (isPermanent)
                backupDir = Path.Combine(backupDir, "permanent");
            var archivePath = Path.Combine(backupDir, $"{game.GameName}_{timestamp}.zip");

            Directory.CreateDirectory(backupDir);

            _logger.LogInformation("Creating{0} backup for game {1} at {2}",
                isPermanent ? " permanent" : string.Empty, game.GameName, archivePath);

            try
            {
                archive = ZipArchive.Create();

                var backupSuccess = false;

                if (Directory.Exists(game.SavePath))
                {
                    await AddToArchiveAsync(archive, game.SavePath, "saves", _cancellationTokenSource.Token, streams);
                    backupSuccess = true;
                }
                else
                {
                    _logger.LogWarning("Save path not found for game {GameName}: {Path}", game.GameName, game.SavePath);
                }

                if (!string.IsNullOrEmpty(game.ModPath) && Directory.Exists(game.ModPath))
                    await AddToArchiveAsync(archive, game.ModPath, "mods", _cancellationTokenSource.Token, streams);

                if (!string.IsNullOrEmpty(game.AddPath) && Directory.Exists(game.AddPath))
                    await AddToArchiveAsync(archive, game.AddPath, "additional", _cancellationTokenSource.Token,
                        streams);

                if (!backupSuccess)
                {
                    _logger.LogWarning("No valid paths found for backup of game {GameName}", game.GameName);
                    archive.Dispose();
                    archive = null;
                    if (File.Exists(archivePath))
                        try
                        {
                            File.Delete(archivePath);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error deleting failed backup file: {Path}", archivePath);
                        }
                }
                else
                {
                    var writerOptions = new WriterOptions(GetSharpCompressCompressionType());

                    await using var fileStream = new FileStream(archivePath, FileMode.Create, FileAccess.Write,
                        FileShare.Read | FileShare.Write);
                    archive.SaveTo(fileStream, writerOptions);

                    if (isPermanent && File.Exists(archivePath))
                        File.SetAttributes(archivePath, File.GetAttributes(archivePath) | FileAttributes.ReadOnly);
                    _logger.LogInformation("Backup created successfully for {GameName}", game.GameName);
                }
            }
            finally
            {
                if (archive != null)
                    try
                    {
                        archive.Dispose();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error disposing archive");
                    }

                foreach (var stream in streams)
                    try
                    {
                        stream.Dispose();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error disposing file stream");
                    }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Backup operation cancelled for {GameName}", game.GameName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating backup for {GameName}", game.GameName);
            throw;
        }
    }

    public async Task RestoreLatestBackupAsync(GameModel game)
    {
        ThrowIfDisposed();

        if (game == null)
            throw new ArgumentNullException(nameof(game));

        var settings = _settingsService.CurrentSettings.BackupSettings;
        var safeName = GetSafeDirectoryName(game.GameName);
        var backupDir = Path.Combine(settings.BackupRootFolder, safeName);

        if (!Directory.Exists(backupDir))
        {
            _logger.LogWarning("Backup directory not found for game {GameName}: {Path}", game.GameName, backupDir);
            return;
        }

        var latestBackup = Directory.GetFiles(backupDir, "*.zip")
            .OrderByDescending(File.GetLastWriteTime)
            .FirstOrDefault();

        if (latestBackup == null)
        {
            _logger.LogWarning("No backups found for game {GameName}", game.GameName);
            return;
        }

        _logger.LogInformation("Restoring latest backup for game {GameName} from {Path}", game.GameName, latestBackup);

        try
        {
            using var archive = ZipArchive.Open(latestBackup);
            foreach (var entry in archive.Entries)
            {
                if (entry.IsDirectory || string.IsNullOrEmpty(entry.Key))
                    continue;

                string? targetRoot = null;
                var relativePath = string.Empty;

                if (entry.Key.StartsWith("saves/"))
                {
                    if (!string.IsNullOrEmpty(game.SavePath))
                    {
                        targetRoot = game.SavePath;
                        relativePath = entry.Key.Substring("saves/".Length);
                    }
                }
                else if (entry.Key.StartsWith("mods/"))
                {
                    if (!string.IsNullOrEmpty(game.ModPath))
                    {
                        targetRoot = game.ModPath;
                        relativePath = entry.Key.Substring("mods/".Length);
                    }
                }
                else if (entry.Key.StartsWith("additional/"))
                {
                    if (!string.IsNullOrEmpty(game.AddPath))
                    {
                        targetRoot = game.AddPath;
                        relativePath = entry.Key.Substring("additional/".Length);
                    }
                }

                if (targetRoot != null && !string.IsNullOrEmpty(relativePath))
                {
                    var entryPath = Path.Combine(targetRoot, relativePath);
                    var directory = Path.GetDirectoryName(entryPath);
                    if (!string.IsNullOrEmpty(directory))
                        Directory.CreateDirectory(directory);

                    try
                    {
                        await using var entryStream = entry.OpenEntryStream();
                        await using var fileStream = new FileStream(entryPath, FileMode.Create, FileAccess.Write,
                            FileShare.Read | FileShare.Write);
                        await entryStream.CopyToAsync(fileStream);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error extracting file {File} for game {GameName}", entryPath,
                            game.GameName);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error restoring backup for {GameName}", game.GameName);
        }
    }

    public void CleanupOldBackups(GameModel? game)
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

    public async Task ProcessSpecialBackup(GameModel? game)
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

    public bool VerifyBackupPaths(GameModel? game)
    {
        if (game == null)
            throw new ArgumentNullException(nameof(game));

        var hasValidPath = false;

        if (!string.IsNullOrWhiteSpace(game.SavePath))
            try
            {
                var fullPath = Path.GetFullPath(game.SavePath);
                if (Directory.Exists(fullPath))
                    hasValidPath = true;
                else
                    _logger.LogWarning("Save path not found for game {GameName}: {Path}",
                        game.GameName, fullPath);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Invalid save path for game {GameName}: {Path}",
                    game.GameName, game.SavePath);
            }

        if (!string.IsNullOrWhiteSpace(game.ModPath))
            try
            {
                var fullPath = Path.GetFullPath(game.ModPath);
                if (Directory.Exists(fullPath))
                    hasValidPath = true;
                else
                    _logger.LogWarning("Mod path not found for game {GameName}: {Path}",
                        game.GameName, fullPath);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Invalid mod path for game {GameName}: {Path}",
                    game.GameName, game.ModPath);
            }

        if (!string.IsNullOrEmpty(game.AddPath))
            try
            {
                var fullPath = Path.GetFullPath(game.AddPath);
                if (Directory.Exists(fullPath))
                    hasValidPath = true;
                else
                    _logger.LogWarning("Additional path not found for game {GameName}: {Path}",
                        game.GameName, fullPath);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Invalid additional path for game {GameName}: {Path}",
                    game.GameName, game.AddPath);
            }

        return hasValidPath;
    }

    public int GetBackupCount(GameModel? game)
    {
        if (game == null)
            throw new ArgumentNullException(nameof(game));

        try
        {
            var safeName = GetSafeDirectoryName(game.GameName);
            var backupDir = Path.Combine(_settingsService.CurrentSettings.BackupSettings.BackupRootFolder, safeName);

            if (!Directory.Exists(backupDir)) return 0;

            return Directory.GetFiles(backupDir, "*.zip").Length;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get backup count for {GameName}", game.GameName);
            return 0;
        }
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
                try
                {
                    _cancellationTokenSource.Cancel();
                    _cancellationTokenSource.Dispose();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during BackupService disposal");
                }

            _disposed = true;
        }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(BackupService));
    }

    private async Task AddToArchiveAsync(ZipArchive archive, string? sourcePath, string entryPrefix,
        CancellationToken cancellationToken, List<FileStream> streams)
    {
        if (sourcePath == null) return;

        foreach (var file in Directory.GetFiles(sourcePath, "*", SearchOption.AllDirectories))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var relativePath = Path.GetRelativePath(sourcePath, file);
            var entryPath = Path.Combine(entryPrefix, relativePath).Replace('\\', '/');

            try
            {
                var fileStream = new FileStream(file, FileMode.Open, FileAccess.Read,
                    FileShare.Read | FileShare.Write);
                streams.Add(fileStream);
                archive.AddEntry(entryPath, fileStream);
            }
            catch (IOException ioEx)
            {
                _logger.LogWarning(ioEx, "File in use, skipping: {File}", file);
            }
            catch (UnauthorizedAccessException uaEx)
            {
                _logger.LogWarning(uaEx, "Access denied, skipping: {File}", file);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding file to archive: {File}", file);
            }
        }

        await Task.CompletedTask;
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

    private string GetSafeDirectoryName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name cannot be null or empty", nameof(name));

        var invalid = Path.GetInvalidFileNameChars().Concat(Path.GetInvalidPathChars()).ToArray();
        return string.Join("_", name.Split(invalid));
    }

    private CompressionType GetSharpCompressCompressionType()
    {
        return _settingsService.CurrentSettings.BackupSettings.CompressionLevel switch
        {
            CompressionLevel.Fastest => CompressionType.None,
            CompressionLevel.Optimal => CompressionType.Deflate,
            CompressionLevel.SmallestSize => CompressionType.BZip2,
            _ => CompressionType.Deflate
        };
    }
}