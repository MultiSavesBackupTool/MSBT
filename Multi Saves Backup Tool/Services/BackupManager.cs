using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Multi_Saves_Backup_Tool.Models;
using Multi_Saves_Backup_Tool.Paths;
using System.Text.Json;

namespace Multi_Saves_Backup_Tool.Services;

public class BackupManager : IDisposable
{
    private static readonly string StateFilePath = AppPaths.ServiceStateFilePath;

    private readonly IBackupService _backupService;
    private readonly IGamesService _gamesService;
    private readonly ILogger<BackupManager> _logger;
    private ServiceState _serviceState;
    private readonly ISettingsService _settingsService;
    private Task? _backupTask;
    private CancellationTokenSource? _cancellationTokenSource;

    public event Action? StateChanged;

    public BackupManager(
        ILogger<BackupManager> logger,
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

    public ServiceState State => _serviceState;

    public void Dispose()
    {
        StopAsync().Wait();
    }

    public async Task StartAsync()
    {
        _logger.LogInformation("Backup manager is starting...");
        _serviceState.ServiceStatus = "Starting";
        SaveServiceState();
        await _settingsService.ReloadSettingsAsync();

        _cancellationTokenSource = new CancellationTokenSource();
        _backupTask = Task.Run(() => ExecuteAsync(_cancellationTokenSource.Token));
    }

    public async Task StopAsync()
    {
        _serviceState.ServiceStatus = "Stopping";
        SaveServiceState();

        if (_cancellationTokenSource != null)
        {
            _cancellationTokenSource.Cancel();
            if (_backupTask != null) await _backupTask;
            _cancellationTokenSource.Dispose();
            _cancellationTokenSource = null;
        }
    }

    private async Task ExecuteAsync(CancellationToken stoppingToken)
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
            _logger.LogError(ex, "Fatal error in backup manager");
            throw;
        }
    }

    private async Task ProcessBackupsAsync(CancellationToken stoppingToken)
    {
        try
        {
            _logger.LogInformation("Starting backup process at: {time}", DateTimeOffset.Now);

            var games = await _gamesService.LoadGamesAsync();
            if (!games.Any())
            {
                _logger.LogWarning("No games found in configuration");
                return;
            }

            var enabledGames = games.Where(g => g.IsEnabled).ToList();
            _logger.LogInformation("Found {Count} enabled games for backup", enabledGames.Count);

            var removedGames = new List<string>();
            foreach (var gameName in _serviceState.GamesState.Keys)
            {
                var game = await _gamesService.GetGameByNameAsync(gameName);
                if (game == null) removedGames.Add(gameName);
            }

            foreach (var gameName in removedGames)
            {
                _logger.LogInformation("Removing state for deleted game: {GameName}", gameName);
                _serviceState.GamesState.Remove(gameName);
            }

            foreach (var game in games)
            {
                if (!_serviceState.GamesState.ContainsKey(game.GameName))
                    _serviceState.GamesState[game.GameName] = new GameState { GameName = game.GameName };

                _serviceState.GamesState[game.GameName].Status = game.IsEnabled ? "Waiting" : "Disabled";
            }

            var settings = _settingsService.CurrentSettings.BackupSettings;
            var tasks = new List<Task>();
            var semaphore = new SemaphoreSlim(settings.MaxParallelBackups);

            foreach (var game in enabledGames)
            {
                if (stoppingToken.IsCancellationRequested)
                {
                    _logger.LogInformation("Backup process cancelled");
                    break;
                }

                var gameState = _serviceState.GamesState[game.GameName];
                if (gameState.NextBackupScheduled > DateTime.Now)
                {
                    _logger.LogInformation(
                        "Skipping backup for {GameName} as it's not time yet. Next backup scheduled at {NextBackup}",
                        game.GameName, gameState.NextBackupScheduled);
                    continue;
                }

                if (!_gamesService.IsGameRunning(game))
                {
                    _logger.LogInformation("Skipping backup for {GameName} as the game is not running", game.GameName);
                    gameState.Status = "Game Not Running";
                    SaveServiceState();
                    continue;
                }

                try
                {
                    await semaphore.WaitAsync(stoppingToken);

                    tasks.Add(Task.Run(async () =>
                    {
                        try
                        {
                            await ProcessGameBackupAsync(game);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error processing backup for game {GameName}", game.GameName);
                            var state = _serviceState.GamesState[game.GameName];
                            state.Status = "Error";
                            state.LastError = ex.Message;
                            SaveServiceState();
                        }
                        finally
                        {
                            semaphore.Release();
                        }
                    }, stoppingToken));
                }
                catch (OperationCanceledException)
                {
                    _logger.LogInformation("Backup operation cancelled for game {GameName}", game.GameName);
                    break;
                }
            }

            if (tasks.Any())
                try
                {
                    await Task.WhenAll(tasks);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during parallel backup execution");
                }

            _serviceState.LastUpdateTime = DateTime.Now;
            SaveServiceState();
            _logger.LogInformation("Backup process completed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during backup process");
            _serviceState.ServiceStatus = $"Error: {ex.Message}";
            SaveServiceState();
        }
    }

    private async Task ProcessGameBackupAsync(GameModel game)
    {
        var existingGame = await _gamesService.GetGameByNameAsync(game.GameName);
        if (existingGame == null)
        {
            _logger.LogWarning("Game {GameName} no longer exists in configuration, skipping backup", game.GameName);
            return;
        }

        var gameState = _serviceState.GamesState[game.GameName];
        try
        {
            _logger.LogInformation("Processing backup for game: {GameName}", game.GameName);
            gameState.Status = "Processing";
            SaveServiceState();

            if (_backupService.VerifyBackupPaths(game))
            {
                await _backupService.ProcessSpecialBackup(game);
                await _backupService.CreateBackupAsync(game);
                _backupService.CleanupOldBackups(game);

                gameState.LastBackupTime = DateTime.Now;
                gameState.Status = "Success";
                gameState.LastError = "";

                var interval = TimeSpan.FromMinutes(game.BackupInterval);
                gameState.NextBackupScheduled = DateTime.Now.Add(interval);

                _logger.LogInformation("Backup completed successfully for game: {GameName}", game.GameName);
            }
            else
            {
                gameState.Status = "Path Error";
                gameState.LastError = "Invalid backup paths";
                _logger.LogWarning("Backup paths verification failed for game: {GameName}", game.GameName);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing backup for game: {GameName}", game.GameName);
            gameState.Status = "Error";
            gameState.LastError = ex.Message;
            throw;
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
            var json = JsonSerializer.Serialize(_serviceState, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(StateFilePath, json);
            StateChanged?.Invoke();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save service state");
        }
    }
}