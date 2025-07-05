using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Multi_Saves_Backup_Tool.Models;
using Multi_Saves_Backup_Tool.Paths;

namespace Multi_Saves_Backup_Tool.Services;

public class BackupManager(
    ILogger<BackupManager> logger,
    ISettingsService settingsService,
    IGamesService gamesService,
    IBackupService backupService)
    : IDisposable
{
    private static readonly string StateFilePath = AppPaths.ServiceStateFilePath;

    private readonly IBackupService _backupService =
        backupService ?? throw new ArgumentNullException(nameof(backupService));

    private readonly IGamesService
        _gamesService = gamesService ?? throw new ArgumentNullException(nameof(gamesService));

    private readonly ILogger<BackupManager> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    private readonly ISettingsService _settingsService =
        settingsService ?? throw new ArgumentNullException(nameof(settingsService));

    private Task? _backupTask;
    private CancellationTokenSource? _cancellationTokenSource;
    private bool _disposed;

    public ServiceState State { get; } = ServiceState.LoadFromFile(StateFilePath);

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    public event Action? StateChanged;

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
                try
                {
                    StopAsync().Wait();
                    _backupService.Dispose();
                    _cancellationTokenSource?.Dispose();
                    State.ServiceStatus = "Stopped";
                    SaveServiceState();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during BackupManager disposal");
                }

            _disposed = true;
        }
    }

    public async Task StartAsync()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(BackupManager));

        _logger.LogInformation("Backup manager is starting...");
        State.ServiceStatus = "Starting";
        SaveServiceState();

        try
        {
            await _settingsService.ReloadSettingsAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to reload settings during startup");
            State.ServiceStatus = "Error: Failed to load settings";
            SaveServiceState();
            throw;
        }

        _cancellationTokenSource = new CancellationTokenSource();
        _backupTask = Task.Run(() => ExecuteAsync(_cancellationTokenSource.Token));
    }

    public async Task StopAsync()
    {
        if (_disposed)
        {
            _logger.LogWarning("Attempted to stop already disposed BackupManager");
            return;
        }

        try
        {
            State.ServiceStatus = "Stopping";
            SaveServiceState();

            if (_cancellationTokenSource != null)
            {
                await Task.Run(() => _cancellationTokenSource.Cancel());
                if (_backupTask != null)
                    try
                    {
                        await Task.WhenAny(_backupTask, Task.Delay(5000));
                        if (!_backupTask.IsCompleted) _logger.LogWarning("Backup task did not complete within timeout");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error waiting for backup task to complete");
                    }

                _cancellationTokenSource.Dispose();
                _cancellationTokenSource = null;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during BackupManager stop");
            throw;
        }
    }

    private async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_disposed)
        {
            _logger.LogWarning("Attempted to execute disposed BackupManager");
            return;
        }

        try
        {
            State.ServiceStatus = "Running";
            SaveServiceState();

            while (!stoppingToken.IsCancellationRequested)
                try
                {
                    await ProcessBackupsAsync(stoppingToken);
                    var interval = _settingsService.CurrentSettings.BackupSettings.GetScanInterval();
                    await Task.Delay(interval, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    _logger.LogInformation("Backup process cancelled");
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during backup process");
                    State.ServiceStatus = $"Error: {ex.Message}";
                    SaveServiceState();

                    await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
                }
        }
        catch (Exception ex)
        {
            State.ServiceStatus = $"Fatal error: {ex.Message}";
            SaveServiceState();
            _logger.LogError(ex, "Fatal error in backup manager");
            throw;
        }
    }

    private async Task ProcessBackupsAsync(CancellationToken stoppingToken)
    {
        if (_disposed)
        {
            _logger.LogWarning("Attempted to process backups after disposal");
            return;
        }

        try
        {
            _logger.LogInformation("Starting backup process at: {time}", DateTimeOffset.Now);

            if (stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("Backup process cancelled before starting");
                return;
            }

            var games = await _gamesService.LoadGamesAsync();
            if (!games.Any())
            {
                _logger.LogWarning("No games found in configuration");
                return;
            }

            var enabledGames = games.Where(g => g.IsEnabled).ToList();
            _logger.LogInformation("Found {Count} enabled games for backup", enabledGames.Count);

            var removedGames = new List<string>();
            foreach (var gameName in State.GamesState.Keys)
            {
                var game = await _gamesService.GetGameByNameAsync(gameName);
                if (game == null) removedGames.Add(gameName);
            }

            foreach (var gameName in removedGames)
            {
                _logger.LogInformation("Removing state for deleted game: {GameName}", gameName);
                State.GamesState.Remove(gameName);
            }

            foreach (var game in games)
            {
                if (!State.GamesState.ContainsKey(game.GameName))
                    State.GamesState[game.GameName] = new GameState { GameName = game.GameName };

                var gameExeName = Path.GetFileNameWithoutExtension(game.GameExe);
                var isRunning = Process.GetProcesses().Any(p =>
                {
                    try
                    {
                        return p.ProcessName.Equals(gameExeName, StringComparison.OrdinalIgnoreCase);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error checking process {ProcessName} for game {GameName}",
                            p.ProcessName, game.GameName);
                        return false;
                    }
                });

                if (!isRunning && !string.IsNullOrEmpty(game.GameExeAlt))
                {
                    var altExeName = Path.GetFileNameWithoutExtension(game.GameExeAlt);
                    isRunning = Process.GetProcesses().Any(p =>
                    {
                        try
                        {
                            return p.ProcessName.Equals(altExeName, StringComparison.OrdinalIgnoreCase);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Error checking process {ProcessName} for alt game exe {GameName}",
                                p.ProcessName, game.GameName);
                            return false;
                        }
                    });
                }

                State.GamesState[game.GameName].Status = game.IsEnabled ? "Waiting" : "Disabled";
                State.GamesState[game.GameName].IsRun = isRunning;
                SaveServiceState();
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

                var gameState = State.GamesState[game.GameName];
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
                            await ProcessGameBackupAsync(game, false);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error processing backup for game {GameName}", game.GameName);
                            var state = State.GamesState[game.GameName];
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

            State.LastUpdateTime = DateTime.Now;
            SaveServiceState();
            _logger.LogInformation("Backup process completed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during backup process");
            State.ServiceStatus = $"Error: {ex.Message}";
            SaveServiceState();
        }
    }

    public async Task ProcessGameBackupAsync(GameModel game, bool isProtected)
    {
        var existingGame = await _gamesService.GetGameByNameAsync(game.GameName);
        if (existingGame == null)
        {
            _logger.LogWarning("Game {GameName} no longer exists in configuration, skipping backup", game.GameName);
            return;
        }

        var gameState = State.GamesState[game.GameName];
        try
        {
            _logger.LogInformation("Processing backup for game: {GameName}", game.GameName);
            gameState.Status = "Processing";
            SaveServiceState();

            if (_backupService.VerifyBackupPaths(game))
            {
                await _backupService.ProcessSpecialBackup(game);
                await _backupService.CreateBackupAsync(game, isProtected);
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
            State.LastUpdateTime = DateTime.Now;
            var json = JsonSerializer.Serialize(State);
            File.WriteAllText(StateFilePath, json);
            StateChanged?.Invoke();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save service state");
        }
    }
}