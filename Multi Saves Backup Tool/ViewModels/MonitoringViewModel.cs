using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Multi_Saves_Backup_Tool.ViewModels;

public class LocalServiceState
{
    public DateTime LastUpdateTime { get; set; }
    public Dictionary<string, LocalGameState> GamesState { get; set; } = new();
    public string ServiceStatus { get; set; } = "Running";

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
    public string Status { get; set; } = "Waiting";
    public DateTime? NextBackupScheduled { get; set; }
}

public class GameMonitoringInfo
{
    public string GameName { get; set; } = "";
    public string LastBackupTime { get; set; } = "No data";
    public string Status { get; set; } = "Waiting";
    public string NextBackupScheduled { get; set; } = "Not scheduled";
}

public class MonitoringViewModel : ViewModelBase
{
    private readonly CancellationTokenSource _cancellationTokenSource;
    private DateTime _lastUpdateTime;
    private string _serviceStatus = "Unknown";

    public MonitoringViewModel()
    {
        _cancellationTokenSource = new CancellationTokenSource();
        _ = StartMonitoring(_cancellationTokenSource.Token);
    }

    public ObservableCollection<GameMonitoringInfo> Games { get; } = new();

    public string ServiceStatus
    {
        get => _serviceStatus;
        set => SetProperty(ref _serviceStatus, value);
    }

    public string LastUpdateTime => _lastUpdateTime.ToString("g");

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
                ServiceStatus = "Service not running";
                return;
            }

            var json = await File.ReadAllTextAsync(statePath);
            var state = JsonSerializer.Deserialize<LocalServiceState>(json) ?? new LocalServiceState();

            ServiceStatus = state.ServiceStatus switch
            {
                "Running" => "Running",
                "Stopped" => "Stopped",
                _ => "Unknown"
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
                        "Success" => "Success",
                        "Waiting" => "Waiting",
                        "Backing up" => "Backing up",
                        "Cleaning" => "Cleaning old backups",
                        _ => gameState.Status
                    },
                    LastBackupTime = gameState.LastBackupTime?.ToString("g") ?? "No data",
                    NextBackupScheduled = gameState.NextBackupScheduled?.ToString("g") ?? "Not scheduled"
                });
        }
        catch (Exception ex)
        {
            ServiceStatus = $"Connection error: {ex.Message}";
            Console.WriteLine($"Error updating state: {ex}");
        }
    }

    public void Dispose()
    {
        _cancellationTokenSource.Cancel();
        _cancellationTokenSource.Dispose();
    }
}