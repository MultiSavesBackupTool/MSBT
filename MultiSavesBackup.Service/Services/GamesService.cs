using System.Diagnostics;
using System.Text.Json;
using Multi_Saves_Backup_Tool.Models;

namespace MultiSavesBackup.Service.Services;

public class GamesService : IGamesService, IDisposable
{
    private readonly SemaphoreSlim _cacheLock = new(1, 1);
    private readonly ILogger<GamesService> _logger;
    private readonly ISettingsService _settingsService;
    private readonly FileSystemWatcher? _watcher;
    private IReadOnlyList<GameModel>? _cachedGames;

    public GamesService(ISettingsService settingsService, ILogger<GamesService> logger)
    {
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        var gamesPath = _settingsService.CurrentSettings.BackupSettings.GetAbsoluteGamesConfigPath();
        var directory = Path.GetDirectoryName(gamesPath);
        var fileName = Path.GetFileName(gamesPath);

        if (directory != null)
            try
            {
                _watcher = new FileSystemWatcher(directory)
                {
                    Filter = fileName,
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.CreationTime | NotifyFilters.Size,
                    EnableRaisingEvents = true
                };

                _watcher.Changed += async (_, args) =>
                {
                    if (args.ChangeType == WatcherChangeTypes.Changed)
                    {
                        _logger.LogInformation("Games configuration file changed, reloading immediately");
                        await ClearCacheAsync();
                        await LoadGamesAsync();
                    }
                };

                _watcher.Created += async (_, _) =>
                {
                    _logger.LogInformation("Games configuration file created, reloading immediately");
                    await ClearCacheAsync();
                    await LoadGamesAsync();
                };

                _watcher.Deleted += async (_, _) =>
                {
                    _logger.LogInformation("Games configuration file deleted, clearing cache");
                    await ClearCacheAsync();
                };

                _watcher.Error += (_, ex) =>
                {
                    _logger.LogError("Error in FileSystemWatcher for games configuration: {Message}",
                        ex.GetException().Message);
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize FileSystemWatcher for games configuration");
            }
        else
            _logger.LogWarning("Could not create FileSystemWatcher for games configuration: directory is null");
    }

    public void Dispose()
    {
        _watcher?.Dispose();
        _cacheLock.Dispose();
    }

    public async Task<IReadOnlyList<GameModel>> LoadGamesAsync()
    {
        try
        {
            await _cacheLock.WaitAsync();
            try
            {
                if (_cachedGames != null) return _cachedGames;

                var gamesPath = _settingsService.CurrentSettings.BackupSettings.GetAbsoluteGamesConfigPath();

                if (!File.Exists(gamesPath))
                {
                    _logger.LogWarning("Games configuration file not found at {Path}", gamesPath);
                    return Array.Empty<GameModel>();
                }

                var json = await File.ReadAllTextAsync(gamesPath);
                var games = JsonSerializer.Deserialize<List<GameModel>>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (games == null)
                {
                    _logger.LogError("Failed to deserialize games from {Path}", gamesPath);
                    return Array.Empty<GameModel>();
                }

                _cachedGames = games;
                _logger.LogInformation("Successfully loaded {Count} games from configuration", games.Count);
                return games;
            }
            finally
            {
                _cacheLock.Release();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading games configuration");
            return Array.Empty<GameModel>();
        }
    }

    public async Task<GameModel?> GetGameByNameAsync(string gameName)
    {
        if (string.IsNullOrWhiteSpace(gameName))
            throw new ArgumentException("Game name cannot be null or whitespace", nameof(gameName));

        var games = await LoadGamesAsync();
        return games.FirstOrDefault(g => g.GameName.Equals(gameName, StringComparison.OrdinalIgnoreCase));
    }

    public bool IsGameRunning(GameModel game)
    {
        if (game == null)
            throw new ArgumentNullException(nameof(game));

        try
        {
            var processes = Process.GetProcesses();
            var gameExeName = Path.GetFileNameWithoutExtension(game.GameExe);

            var isMainExeRunning = processes.Any(p =>
            {
                try
                {
                    return p.ProcessName.Equals(gameExeName, StringComparison.OrdinalIgnoreCase);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error checking process {ProcessName}", p.ProcessName);
                    return false;
                }
            });

            if (isMainExeRunning) return true;

            if (!string.IsNullOrEmpty(game.GameExeAlt))
            {
                var altExeName = Path.GetFileNameWithoutExtension(game.GameExeAlt);
                return processes.Any(p =>
                {
                    try
                    {
                        return p.ProcessName.Equals(altExeName, StringComparison.OrdinalIgnoreCase);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error checking process {ProcessName}", p.ProcessName);
                        return false;
                    }
                });
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking if game is running");
            return false;
        }
    }

    private async Task ClearCacheAsync()
    {
        await _cacheLock.WaitAsync();
        try
        {
            _cachedGames = null;
        }
        finally
        {
            _cacheLock.Release();
        }
    }
}