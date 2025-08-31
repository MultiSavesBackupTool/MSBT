using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Timers;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Multi_Saves_Backup_Tool.Models;
using Multi_Saves_Backup_Tool.Services;
using Properties;

namespace Multi_Saves_Backup_Tool.ViewModels;

public class GameMonitoringInfo : ObservableObject
{
    private string? _gameName = "";
    private string _gamesCountArchives = "0";
    private string _gamesSizesArchives = "0 MB";
    private string _lastBackupTime = Resources.NoData;
    private string _nextBackupScheduled = Resources.NotScheduled;
    private bool _showArchiveCount;
    private bool _showArchiveSize;
    private bool _showNextBackup;
    private bool _showStatusBadge;
    private string _status = Resources.StatusWaiting;

    public string? GameName
    {
        get => _gameName;
        set => SetProperty(ref _gameName, value);
    }

    public string LastBackupTime
    {
        get => _lastBackupTime;
        set => SetProperty(ref _lastBackupTime, value);
    }

    public string Status
    {
        get => _status;
        set => SetProperty(ref _status, value);
    }

    public string NextBackupScheduled
    {
        get => _nextBackupScheduled;
        set => SetProperty(ref _nextBackupScheduled, value);
    }

    public string GamesCountArchives
    {
        get => _gamesCountArchives;
        set => SetProperty(ref _gamesCountArchives, value);
    }

    public string GamesSizesArchives
    {
        get => _gamesSizesArchives;
        set => SetProperty(ref _gamesSizesArchives, value);
    }

    public bool ShowArchiveCount
    {
        get => _showArchiveCount;
        set => SetProperty(ref _showArchiveCount, value);
    }

    public bool ShowArchiveSize
    {
        get => _showArchiveSize;
        set => SetProperty(ref _showArchiveSize, value);
    }

    public bool ShowStatusBadge
    {
        get => _showStatusBadge;
        set => SetProperty(ref _showStatusBadge, value);
    }

    public bool ShowNextBackup
    {
        get => _showNextBackup;
        set => SetProperty(ref _showNextBackup, value);
    }
}

public class MonitoringViewModel : ViewModelBase, IDisposable
{
    private readonly BackupManager _backupManager;
    private readonly IBackupService _backupService;
    private readonly IGamesService _gamesService;
    private readonly ISettingsService _settingsService;
    private string _archivesCounts = "0";
    private DateTime _lastUpdateTime;
    private string _serviceStatus = Resources.StatusUnknown;
    private bool _showArchiveCount = true;
    private bool _showArchiveSize = true;
    private bool _showNextBackup = true;
    private bool _showStatusBadge = true;
    private string _sizesArchives = "0 MB";
    private Timer? _statsTimer;

    public MonitoringViewModel(BackupManager backupManager, IGamesService gamesService, IBackupService backupService,
        ISettingsService settingsService, ILogger<MonitoringViewModel>? logger = null) : base(logger)
    {
        _backupManager = backupManager;
        _gamesService = gamesService;
        _backupService = backupService;
        _settingsService = settingsService;
        _backupManager.StateChanged += OnStateChanged;
        RestartServiceCommand = new AsyncRelayCommand(RestartServiceAsync);
        UpdateState();
        StartStatsTimer();
    }

    public IAsyncRelayCommand RestartServiceCommand { get; }

    public ObservableCollection<GameMonitoringInfo> Games { get; } = new();

    public string ServiceStatus
    {
        get => _serviceStatus;
        set => SetProperty(ref _serviceStatus, value);
    }

    public string LastUpdateTime => _lastUpdateTime.ToString("g");

    public string? CurrentGameName { get; private set; }

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

    public bool ShowArchiveCount
    {
        get => _showArchiveCount;
        set
        {
            if (SetProperty(ref _showArchiveCount, value))
                foreach (var game in Games)
                    game.ShowArchiveCount = value;
        }
    }

    public bool ShowArchiveSize
    {
        get => _showArchiveSize;
        set
        {
            if (SetProperty(ref _showArchiveSize, value))
                foreach (var game in Games)
                    game.ShowArchiveSize = value;
        }
    }

    public bool ShowNextBackup
    {
        get => _showNextBackup;
        set
        {
            if (SetProperty(ref _showNextBackup, value))
                foreach (var game in Games)
                    game.ShowNextBackup = value;
        }
    }

    public bool ShowStatusBadge
    {
        get => _showStatusBadge;
        set
        {
            if (SetProperty(ref _showStatusBadge, value))
                foreach (var game in Games)
                    game.ShowStatusBadge = value;
        }
    }

    public void Dispose()
    {
        _backupManager.StateChanged -= OnStateChanged;
        _statsTimer?.Dispose();
    }

    private void OnStateChanged()
    {
        Dispatcher.UIThread.Post(UpdateState);
    }

    private async Task RestartServiceAsync()
    {
        try
        {
            await _backupManager.StopAsync();
            await _backupManager.StartAsync();
            UpdateState();
        }
        catch (Exception ex)
        {
            ServiceStatus = string.Format(Resources.StatusConnectionError, ex.Message);
        }
    }

    private void StartStatsTimer()
    {
        _statsTimer?.Dispose();
        var interval = _settingsService.CurrentSettings.BackupSettings.GetScanInterval();
        _statsTimer = new Timer(interval.TotalMilliseconds);
        _statsTimer.Elapsed += (_, _) => Dispatcher.UIThread.Post(UpdateStatsAsync);
        _statsTimer.AutoReset = true;
        _statsTimer.Start();
    }

    private void UpdateState()
    {
        try
        {
            var state = _backupManager.State;

            ServiceStatus = state.ServiceStatus switch
            {
                "Running" => Resources.StatusRunning,
                "Stopped" => Resources.StatusStopped,
                _ => Resources.StatusUnknown
            };

            _lastUpdateTime = state.LastUpdateTime;
            OnPropertyChanged(nameof(LastUpdateTime));

            var gamesFromState = state.GamesState.Values.ToList();
            var gamesInVm = Games.ToList();

            foreach (var gameInVm in gamesInVm.Where(gameInVm =>
                         gamesFromState.All(gfs => gfs.GameName != gameInVm.GameName))) Games.Remove(gameInVm);

            foreach (var gameState in gamesFromState)
            {
                var existingGame = Games.FirstOrDefault(g => g.GameName == gameState.GameName);
                if (existingGame != null)
                {
                    UpdateGameMonitoringInfo(existingGame, gameState);
                }
                else
                {
                    var newGameInfo = new GameMonitoringInfo();
                    UpdateGameMonitoringInfo(newGameInfo, gameState);
                    Games.Add(newGameInfo);
                }
            }

            var sortedGames = Games.OrderBy(g => g.GameName).ToList();
            for (var i = 0; i < sortedGames.Count; i++)
                if (!Equals(Games[i], sortedGames[i]))
                    Games.Move(Games.IndexOf(sortedGames[i]), i);

            var runningGame = state.GamesState.Values.FirstOrDefault(g => g.IsRun);
            CurrentGameName = runningGame?.GameName;
            OnPropertyChanged(nameof(CurrentGameName));

            UpdateStatsAsync();
        }
        catch (Exception ex)
        {
            ServiceStatus = string.Format(Resources.StatusConnectionError, ex.Message);
            CurrentGameName = null;
            OnPropertyChanged(nameof(CurrentGameName));
        }
    }

    private async void UpdateStatsAsync()
    {
        try
        {
            var games = await _gamesService.LoadGamesAsync();
            var totalArchives = 0;
            var totalSize = 0L;

            foreach (var gameInfo in Games)
            {
                var game = games.FirstOrDefault(g => g != null && g.GameName == gameInfo.GameName);
                if (game != null)
                {
                    var backupCount = _backupService.GetBackupCount(game);
                    var gameSize = CalculateGameBackupsSize(game);
                    gameInfo.GamesCountArchives = backupCount.ToString();
                    gameInfo.GamesSizesArchives = FormatSize(gameSize);
                    totalArchives += backupCount;
                    totalSize += gameSize;
                }
                else
                {
                    gameInfo.GamesCountArchives = "0";
                    gameInfo.GamesSizesArchives = "0 MB";
                }
            }

            ArchivesCounts = totalArchives.ToString();
            SizesArchives = FormatSize(totalSize);
        }
        catch
        {
            // UwU
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
        catch
        {
            return 0;
        }
    }

    private string FormatSize(long bytes)
    {
        string[] sizes = ["B", "KB", "MB", "GB"];
        var order = 0;
        double size = bytes;
        while (size >= 1024 && order < sizes.Length - 1)
        {
            order++;
            size /= 1024;
        }

        return $"{size:0.##} {sizes[order]}";
    }

    private string GetSafeDirectoryName(string? name)
    {
        return string.Join("_", name?.Split(Path.GetInvalidFileNameChars()) ?? []);
    }

    private void UpdateGameMonitoringInfo(GameMonitoringInfo info, GameState state)
    {
        info.GameName = state.GameName;
        info.Status = state.Status switch
        {
            "Success" => Resources.StatusSuccess,
            "Running" => Resources.StatusRunning,
            "Processing" => Resources.StatusBackingUp,
            "Cleaning" => Resources.StatusCleaning,
            "Waiting" => Resources.StatusWaiting,
            "Disabled" => Resources.StatusDisabled,
            "Game Not Running" => Resources.StatusGameNotRunning,
            "Error" => Resources.StatusError,
            "Path Error" => Resources.StatusPathError,
            _ => state.Status
        };
        info.LastBackupTime = state.LastBackupTime?.ToString("g") ?? Resources.NoData;
        info.NextBackupScheduled = state.NextBackupScheduled?.ToString("g") ?? Resources.NotScheduled;
        info.ShowArchiveCount = ShowArchiveCount;
        info.ShowArchiveSize = ShowArchiveSize;
        info.ShowStatusBadge = ShowStatusBadge;
        info.ShowNextBackup = ShowNextBackup;
    }
}