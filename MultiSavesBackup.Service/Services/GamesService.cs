using System.Diagnostics;
using System.Text.Json;
using Multi_Saves_Backup_Tool.Models;
using MultiSavesBackup.Service.Models;
using Microsoft.Extensions.Logging;

namespace MultiSavesBackup.Service.Services;

public class GamesService : IGamesService, IDisposable
{
    private readonly ISettingsService _settingsService;
    private readonly ILogger<GamesService> _logger;
    private IReadOnlyList<GameModel>? _cachedGames;
    private readonly FileSystemWatcher? _watcher;

    public GamesService(ISettingsService settingsService, ILogger<GamesService> logger)
    {
        _settingsService = settingsService;
        _logger = logger;

        var gamesPath = _settingsService.CurrentSettings.BackupSettings.GetAbsoluteGamesConfigPath();
        var directory = Path.GetDirectoryName(gamesPath);
        var fileName = Path.GetFileName(gamesPath);

        if (directory != null)
        {
            _watcher = new FileSystemWatcher(directory)
            {
                Filter = fileName,
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.CreationTime,
                EnableRaisingEvents = true
            };

            _watcher.Changed += (_, _) =>
            {
                _logger.LogInformation("Games configuration file changed, clearing cache");
                _cachedGames = null;
            };
        }
        else
        {
            _logger.LogWarning("Could not create FileSystemWatcher for games configuration: directory is null");
        }
    }

    public async Task<IReadOnlyList<GameModel>> LoadGamesAsync()
    {
        try
        {
            if (_cachedGames != null)
                return _cachedGames;

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
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading games configuration");
            return Array.Empty<GameModel>();
        }
    }

    public async Task<GameModel?> GetGameByNameAsync(string gameName)
    {
        var games = await LoadGamesAsync();
        return games.FirstOrDefault(g => g.GameName.Equals(gameName, StringComparison.OrdinalIgnoreCase));
    }

    public bool IsGameRunning(GameModel game)
    {
        try
        {
            var processes = Process.GetProcesses();
            var gameExeName = Path.GetFileNameWithoutExtension(game.GameExe);
            
            var isMainExeRunning = processes.Any(p => p.ProcessName.Equals(gameExeName, StringComparison.OrdinalIgnoreCase));
            
            if (isMainExeRunning)
            {
                return true;
            }

            if (!string.IsNullOrEmpty(game.GameExeAlt))
            {
                var altExeName = Path.GetFileNameWithoutExtension(game.GameExeAlt);
                return processes.Any(p => p.ProcessName.Equals(altExeName, StringComparison.OrdinalIgnoreCase));
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking if game is running");
            return false;
        }
    }

    public void Dispose()
    {
        _watcher?.Dispose();
    }
}
