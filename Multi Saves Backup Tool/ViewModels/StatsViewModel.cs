using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;
using Multi_Saves_Backup_Tool.Models;
using Multi_Saves_Backup_Tool.Services;
using Properties;

namespace Multi_Saves_Backup_Tool.ViewModels;

public class StatsViewModel : ViewModelBase, IDisposable
{
    private readonly IGamesService _gameService;
    private readonly DispatcherTimer _updateTimer;
    private string _archivesCounts = "0";
    private string _sizesArchives = "0 MB";
    private ObservableCollection<GameStatsInfo> _games = new();

    public StatsViewModel(IGamesService gameService)
    {
        _gameService = gameService;
        
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

    public ObservableCollection<GameStatsInfo> Games
    {
        get => _games;
        private set => SetProperty(ref _games, value);
    }

    private async Task UpdateStatsAsync()
    {
        var games = await _gameService.LoadGamesAsync();
        var totalArchives = 0L;
        var totalSize = 0L;

        Games.Clear();
        foreach (var game in games)
        {
            // Since we don't have a direct method to get archives, we'll need to add it
            // For now, we'll just show game information
            Games.Add(new GameStatsInfo
            {
                GameName = game.GameName,
                GamesCountArchives = "0", // This needs to be implemented when we have archive functionality
                GamesSizesArchives = "0 B" // This needs to be implemented when we have archive functionality
            });
        }

        ArchivesCounts = totalArchives.ToString();
        SizesArchives = FormatSize(totalSize);
    }

    private string FormatSize(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB" };
        int order = 0;
        double size = bytes;
        
        while (size >= 1024 && order < sizes.Length - 1)
        {
            order++;
            size /= 1024;
        }

        return $"{size:0.##} {sizes[order]}";
    }

    public void Dispose()
    {
        _updateTimer.Stop();
    }
}

public class GameStatsInfo
{
    public string GameName { get; set; } = "";
    public string GamesCountArchives { get; set; } = "";
    public string GamesSizesArchives { get; set; } = "";
}