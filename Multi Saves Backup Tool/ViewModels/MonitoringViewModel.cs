using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Properties;

namespace Multi_Saves_Backup_Tool.ViewModels;

public class LocalServiceState
{
    public DateTime LastUpdateTime { get; set; }
    public Dictionary<string, LocalGameState> GamesState { get; set; } = new();
    public string ServiceStatus { get; set; } = Resources.StatusServiceRunning;

    public static LocalServiceState LoadFromFile(string path)
    {
        if (!File.Exists(path))
            return new LocalServiceState();

        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<LocalServiceState>(json) ?? new LocalServiceState();
    }
}

public class LocalGameState
{
    public string GameName { get; set; } = "";
    public DateTime? LastBackupTime { get; set; }
    public string Status { get; set; } = Resources.StatusServiceRunning;
    public DateTime? NextBackupScheduled { get; set; }
}

public class GameMonitoringInfo
{
    public string GameName { get; set; } = "";
    public string LastBackupTime { get; set; } = Resources.NoData;
    public string Status { get; set; } = Resources.StatusWaiting;
    public string NextBackupScheduled { get; set; } = Resources.NotScheduled;
}

public class MonitoringViewModel : ViewModelBase, IDisposable
{
    private readonly CancellationTokenSource _cancellationTokenSource;
    private readonly Task _monitoringTask;
    private DateTime _lastUpdateTime;
    private string _serviceStatus = Resources.StatusUnknown;

    public MonitoringViewModel()
    {
        _cancellationTokenSource = new CancellationTokenSource();
        _monitoringTask = StartMonitoring(_cancellationTokenSource.Token);
    }

    public ObservableCollection<GameMonitoringInfo> Games { get; } = new();

    public string ServiceStatus
    {
        get => _serviceStatus;
        set => SetProperty(ref _serviceStatus, value);
    }

    public string LastUpdateTime => _lastUpdateTime.ToString("g");

    public void Dispose()
    {
        _cancellationTokenSource.Cancel();
        try
        {
            _monitoringTask.Wait(TimeSpan.FromSeconds(5));
        }
        catch (AggregateException)
        {
            // Task was canceled, which is expected
        }
        _cancellationTokenSource.Dispose();
    }

    private async Task StartMonitoring(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            await UpdateServiceState();
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
            }
            catch (TaskCanceledException)
            {
                break;
            }
        }
    }

    private async Task UpdateServiceState()
    {
        try
        {
            var statePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "service_state.json");
            if (!File.Exists(statePath))
            {
                ServiceStatus = Resources.StatusServiceNotRunning;
                return;
            }

            var json = await File.ReadAllTextAsync(statePath);
            var state = JsonSerializer.Deserialize<LocalServiceState>(json) ?? new LocalServiceState();

            ServiceStatus = state.ServiceStatus switch
            {
                var s when s == Resources.StatusServiceRunning => Resources.StatusRunning,
                var s when s == Resources.StatusServiceStopped => Resources.StatusStopped,
                _ => Resources.StatusUnknown
            };

            _lastUpdateTime = state.LastUpdateTime;
            OnPropertyChanged(nameof(LastUpdateTime));

            Games.Clear();
            foreach (var (_, gameState) in state.GamesState)
                Games.Add(new GameMonitoringInfo
                {
                    GameName = gameState.GameName,
                    Status = gameState.Status switch
                    {
                        var s when s == Resources.StatusServiceSuccess => Resources.StatusSuccess,
                        var s when s == Resources.StatusServiceRunning => Resources.StatusRunning,
                        var s when s == Resources.StatusServiceBackingUp => Resources.StatusBackingUp,
                        var s when s == Resources.StatusServiceCleaning => Resources.StatusCleaning,
                        var s when s == Resources.StatusServiceWaiting => Resources.StatusWaiting,
                        _ => gameState.Status
                    },
                    LastBackupTime = gameState.LastBackupTime?.ToString("g") ?? Resources.NoData,
                    NextBackupScheduled = gameState.NextBackupScheduled?.ToString("g") ?? Resources.NotScheduled
                });
        }
        catch (Exception ex)
        {
            ServiceStatus = string.Format(Resources.StatusConnectionError, ex.Message);
            Console.WriteLine($"Error updating state: {ex}");
        }
    }
}