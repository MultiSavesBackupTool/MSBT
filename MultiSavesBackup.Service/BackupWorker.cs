using Multi_Saves_Backup_Tool.Models;
using MultiSavesBackup.Service.Models;
using MultiSavesBackup.Service.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MultiSavesBackup.Service;

public class BackupWorker : BackgroundService
{
    private readonly ILogger<BackupWorker> _logger;
    private readonly ISettingsService _settingsService;
    private readonly IGamesService _gamesService;
    private readonly IBackupService _backupService;
    private ServiceState _serviceState;
    private const string StateFilePath = "service_state.json";

    public BackupWorker(
        ILogger<BackupWorker> logger,
        ISettingsService settingsService,
        IGamesService gamesService,
        IBackupService backupService)
    {
        _logger = logger;
        _settingsService = settingsService;
        _gamesService = gamesService;
        _backupService = backupService;
        _serviceState = ServiceState.LoadFromFile(StateFilePath);
    }

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Backup service is starting...");
        _serviceState.ServiceStatus = "Starting";
        SaveServiceState();
        await _settingsService.ReloadSettingsAsync();
        await base.StartAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            _serviceState.ServiceStatus = "Running";
            SaveServiceState();

            while (!stoppingToken.IsCancellationRequested)
            {
                await ProcessBackupsAsync(stoppingToken);
                var interval = _settingsService.CurrentSettings.BackupSettings.GetScanInterval();
                await Task.Delay(interval, stoppingToken);
            }
        }
        catch (Exception ex)
        {
            _serviceState.ServiceStatus = $"Error: {ex.Message}";
            SaveServiceState();
            _logger.LogError(ex, "Fatal error in backup service");
            throw;
        }
    }

    private async Task ProcessBackupsAsync(CancellationToken stoppingToken)
    {
        try
        {
            _logger.LogInformation("Starting backup process at: {time}", DateTimeOffset.Now);
            
            var games = await _gamesService.LoadGamesAsync();
            var enabledGames = games.Where(g => g.IsEnabled).ToList();
            
            _logger.LogInformation("Found {Count} enabled games for backup", enabledGames.Count);

            // Обновляем состояние для всех игр
            foreach (var game in games)
            {
                if (!_serviceState.GamesState.ContainsKey(game.GameName))
                {
                    _serviceState.GamesState[game.GameName] = new GameState { GameName = game.GameName };
                }
                
                _serviceState.GamesState[game.GameName].Status = game.IsEnabled ? "Waiting" : "Disabled";
            }

            var settings = _settingsService.CurrentSettings.BackupSettings;
            var tasks = new List<Task>();
            var semaphore = new SemaphoreSlim(settings.MaxParallelBackups);

            foreach (var game in enabledGames)
            {
                if (stoppingToken.IsCancellationRequested)
                    break;

                await semaphore.WaitAsync(stoppingToken);

                tasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        await ProcessGameBackupAsync(game);
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                }, stoppingToken));
            }

            await Task.WhenAll(tasks);
            _serviceState.LastUpdateTime = DateTime.Now;
            SaveServiceState();
            _logger.LogInformation("Backup process completed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during backup process");
        }
    }

    private async Task ProcessGameBackupAsync(GameModel game)
    {
        var gameState = _serviceState.GamesState[game.GameName];
        try
        {
            _logger.LogInformation("Processing backup for game: {GameName}", game.GameName);
            gameState.Status = "Processing";
            SaveServiceState();

            if (_backupService.VerifyBackupPaths(game))
            {
                await _backupService.CreateBackupAsync(game);
                _backupService.CleanupOldBackups(game);
                
                gameState.LastBackupTime = DateTime.Now;
                gameState.Status = "Success";
                gameState.LastError = "";
                
                // Вычисляем время следующего бэкапа
                var interval = TimeSpan.FromMinutes(game.BackupInterval);
                gameState.NextBackupScheduled = DateTime.Now.Add(interval);
            }
            else
            {
                gameState.Status = "Path Error";
                gameState.LastError = "Invalid backup paths";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing backup for game: {GameName}", game.GameName);
            gameState.Status = "Error";
            gameState.LastError = ex.Message;
        }
        finally
        {
            SaveServiceState();
        }
    }

    private void SaveServiceState()
    {
        try
        {
            _serviceState.SaveToFile(StateFilePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save service state");
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _serviceState.ServiceStatus = "Stopping";
        SaveServiceState();
        await base.StopAsync(cancellationToken);
    }
}
