using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MultiSavesBackup.Service.Services;

namespace MultiSavesBackup.Service;

public class BackupWorker : BackgroundService
{
    private readonly ILogger<BackupWorker> _logger;
    private readonly ISettingsService _settingsService;
    private readonly IGamesService _gamesService;
    private readonly IBackupService _backupService;

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
    }

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Backup service is starting...");
        await _settingsService.ReloadSettingsAsync();
        await base.StartAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await ProcessBackupsAsync(stoppingToken);
                var interval = _settingsService.CurrentSettings.BackupSettings.GetScanInterval();
                await Task.Delay(interval, stoppingToken);
            }
        }
        catch (Exception ex)
        {
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
            _logger.LogInformation("Backup process completed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during backup process");
        }
    }

    private async Task ProcessGameBackupAsync(Multi_Saves_Backup_Tool.Models.GameModel game)
    {
        try
        {
            _logger.LogInformation("Processing backup for game: {GameName}", game.GameName);

            // Проверяем, не запущена ли игра
            if (await _gamesService.IsGameRunningAsync(game))
            {
                _logger.LogInformation("Skipping backup for {GameName} as it is currently running", game.GameName);
                return;
            }

            // Проверяем существование всех путей
            if (!await _backupService.VerifyBackupPathsAsync(game))
            {
                _logger.LogWarning("Skipping backup for {GameName} due to missing paths", game.GameName);
                return;
            }

            // Создаем резервную копию
            await _backupService.CreateBackupAsync(game);

            // Очищаем старые копии
            await _backupService.CleanupOldBackupsAsync(game);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing backup for game {GameName}", game.GameName);
        }
    }
}
