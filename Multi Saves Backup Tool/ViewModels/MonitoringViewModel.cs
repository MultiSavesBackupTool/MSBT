using System;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Threading;
using Microsoft.Extensions.Logging;
using Multi_Saves_Backup_Tool.Models;
using Multi_Saves_Backup_Tool.Services;
using Properties;

namespace Multi_Saves_Backup_Tool.ViewModels;

public class GameMonitoringInfo
{
    public string? GameName { get; set; } = "";
    public string LastBackupTime { get; set; } = Resources.NoData;
    public string Status { get; set; } = Resources.StatusWaiting;
    public string NextBackupScheduled { get; set; } = Resources.NotScheduled;
}

public class MonitoringViewModel : ViewModelBase, IDisposable
{
    private readonly BackupManager _backupManager;
    private DateTime _lastUpdateTime;
    private string _serviceStatus = Resources.StatusUnknown;

    public MonitoringViewModel(BackupManager backupManager, ILogger<MonitoringViewModel>? logger = null) : base(logger)
    {
        _backupManager = backupManager;
        _backupManager.StateChanged += OnStateChanged;
        UpdateState();
    }

    public ObservableCollection<GameMonitoringInfo> Games { get; } = new();

    public string ServiceStatus
    {
        get => _serviceStatus;
        set => SetProperty(ref _serviceStatus, value);
    }

    public string LastUpdateTime => _lastUpdateTime.ToString("g");

    public string? CurrentGameName { get; private set; }

    public void Dispose()
    {
        _backupManager.StateChanged -= OnStateChanged;
    }

    private void OnStateChanged()
    {
        Dispatcher.UIThread.Post(UpdateState);
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

            var runningGame = state.GamesState.Values.FirstOrDefault(g => g.IsRun);
            CurrentGameName = runningGame?.GameName;
            OnPropertyChanged(nameof(CurrentGameName));
        }
        catch (Exception ex)
        {
            ServiceStatus = string.Format(Resources.StatusConnectionError, ex.Message);
            CurrentGameName = null;
            OnPropertyChanged(nameof(CurrentGameName));
        }
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
    }
}