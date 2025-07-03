using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;
using Microsoft.Extensions.Logging;
using Multi_Saves_Backup_Tool.Models;
using Multi_Saves_Backup_Tool.Services;

namespace Multi_Saves_Backup_Tool.ViewModels;

public class StatsViewModel : ViewModelBase, IDisposable
{
    private readonly IBackupService _backupService;
    private readonly IGamesService _gameService;
    private readonly ILogger<StatsViewModel> _logger;
    private readonly ISettingsService _settingsService;
    private readonly DispatcherTimer _updateTimer;
    private string _archivesCounts = "0";
    private string _sizesArchives = "0 MB";

    public StatsViewModel(IGamesService gameService, IBackupService backupService, ISettingsService settingsService,
        ILogger<StatsViewModel> logger)
    {
        _gameService = gameService;
        _backupService = backupService;
        _settingsService = settingsService;
        _logger = logger;

        _updateTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(5)
        };
        _updateTimer.Tick += async (_, _) => await UpdateStatsAsync();
        _updateTimer.Start();

        _ = UpdateStatsAsync();
    }

    public string ArchivesCounts
    {
        get => _archivesCounts;
        private set => SetProperty(ref _archivesCounts, value);
    }

    public string SizesArchives
    {
        get => _sizesArchives;
        private set => SetProperty(ref _sizesArchives, value);
    }

    public ObservableCollection<GameStatsInfo> Games { get; } = new();

    public void Dispose()
    {
        _updateTimer.Stop();
    }

    private async Task UpdateStatsAsync()
    {
        try
        {
            var games = await _gameService.LoadGamesAsync();

            var totalArchives = 0;
            var totalSize = 0L;

            Games.Clear();
            foreach (var game in games)
            {
                var backupCount = _backupService.GetBackupCount(game);
                var gameSize = CalculateGameBackupsSize(game);

                var specialArchivesCount = GetSpecialArchivesCount(game);
                var specialArchivesSize = CalculateSpecialArchivesSize(game);

                backupCount += specialArchivesCount;
                gameSize += specialArchivesSize;

                totalArchives += backupCount;
                totalSize += gameSize;

                Games.Add(new GameStatsInfo
                {
                    GameName = game.GameName,
                    GamesCountArchives = backupCount.ToString(),
                    GamesSizesArchives = FormatSize(gameSize)
                });
            }

            ArchivesCounts = totalArchives.ToString();
            SizesArchives = FormatSize(totalSize);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating statistics");
        }
    }

    private int GetSpecialArchivesCount(GameModel game)
    {
        try
        {
            var safeName = GetSafeDirectoryName(game.GameName);
            var specialArchiveDir = Path.Combine(_settingsService.CurrentSettings.BackupSettings.BackupRootFolder,
                safeName, "SpecialArchive");

            if (!Directory.Exists(specialArchiveDir)) return 0;

            var count = Directory.GetDirectories(specialArchiveDir).Length;
            return count;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error counting special archives for {GameName}", game.GameName);
            return 0;
        }
    }

    private long CalculateGameBackupsSize(GameModel game)
    {
        try
        {
            var safeName = GetSafeDirectoryName(game.GameName);
            var backupDir = Path.Combine(_settingsService.CurrentSettings.BackupSettings.BackupRootFolder, safeName);
            if (!Directory.Exists(backupDir)) return 0;

            var files = Directory.GetFiles(backupDir, "*.zip");
            var size = files.Sum(file => new FileInfo(file).Length);
            return size;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating backup size for {GameName}", game.GameName);
            return 0;
        }
    }

    private long CalculateSpecialArchivesSize(GameModel game)
    {
        try
        {
            var safeName = GetSafeDirectoryName(game.GameName);
            var specialArchiveDir = Path.Combine(_settingsService.CurrentSettings.BackupSettings.BackupRootFolder,
                safeName, "SpecialArchive");
            if (!Directory.Exists(specialArchiveDir)) return 0;

            var size = Directory.GetDirectories(specialArchiveDir)
                .Sum(dir => Directory.GetFiles(dir, "*.*", SearchOption.AllDirectories)
                    .Sum(file => new FileInfo(file).Length));
            return size;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating special archives size for {GameName}", game.GameName);
            return 0;
        }
    }

    private string FormatSize(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB" };
        var order = 0;
        double size = bytes;

        while (size >= 1024 && order < sizes.Length - 1)
        {
            order++;
            size /= 1024;
        }

        return $"{size:0.##} {sizes[order]}";
    }

    private string GetSafeDirectoryName(string name)
    {
        return string.Join("_", name.Split(Path.GetInvalidFileNameChars()));
    }
}

public class GameStatsInfo
{
    public string GameName { get; set; } = "";
    public string GamesCountArchives { get; set; } = "";
    public string GamesSizesArchives { get; set; } = "";
}