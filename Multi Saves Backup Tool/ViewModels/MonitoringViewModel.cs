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
    public string LastBackupTime { get; set; } = "Нет данных";
    public string Status { get; set; } = "Ожидание";
    public string NextBackupScheduled { get; set; } = "Не запланировано";
}

public class MonitoringViewModel : ViewModelBase
{
    private string _serviceStatus = "Неизвестно";
    private DateTime _lastUpdateTime;
    private readonly CancellationTokenSource _cancellationTokenSource;
    public ObservableCollection<GameMonitoringInfo> Games { get; } = new();
    
    public string ServiceStatus
    {
        get => _serviceStatus;
        set => SetProperty(ref _serviceStatus, value);
    }
    
    public string LastUpdateTime => _lastUpdateTime.ToString("g");
    
    public MonitoringViewModel()
    {
        _cancellationTokenSource = new CancellationTokenSource();
        _ = StartMonitoring(_cancellationTokenSource.Token);
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
                ServiceStatus = "Сервис не запущен";
                return;
            }

            var json = await File.ReadAllTextAsync(statePath);
            var state = JsonSerializer.Deserialize<LocalServiceState>(json) ?? new LocalServiceState();
            
            ServiceStatus = state.ServiceStatus switch
            {
                "Running" => "Работает",
                "Stopped" => "Остановлен",
                _ => "Неизвестно"
            };
            
            _lastUpdateTime = state.LastUpdateTime;
            OnPropertyChanged(nameof(LastUpdateTime));

            Games.Clear();
            foreach (var (_, gameState) in state.GamesState)
            {
                Games.Add(new GameMonitoringInfo
                {
                    GameName = gameState.GameName,
                    Status = gameState.Status switch
                    {
                        "Success" => "Успешно",
                        "Waiting" => "Ожидание",
                        "Backing up" => "Создание резервной копии",
                        "Cleaning" => "Очистка старых копий",
                        _ => gameState.Status
                    },
                    LastBackupTime = gameState.LastBackupTime?.ToString("g") ?? "Нет данных",
                    NextBackupScheduled = gameState.NextBackupScheduled?.ToString("g") ?? "Не запланировано"
                });
            }
        }
        catch (Exception ex)
        {
            ServiceStatus = $"Ошибка подключения: {ex.Message}";
            Console.WriteLine($"Ошибка при обновлении состояния: {ex}");
        }
    }

    public void Dispose()
    {
        _cancellationTokenSource.Cancel();
        _cancellationTokenSource.Dispose();
    }
}
