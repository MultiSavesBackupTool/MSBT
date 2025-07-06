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

public class GamesService : IGamesService, IDisposable
{
    private readonly SemaphoreSlim _cacheLock = new(1, 1);
    private readonly ILogger<GamesService> _logger;
    private IReadOnlyList<GameModel?>? _cachedGames;
    private bool _isSaving;
    private FileSystemWatcher? _watcher;

    public GamesService(ILogger<GamesService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        var gamesPath = AppPaths.GamesFilePath;
        var directory = Path.GetDirectoryName(gamesPath);
        var fileName = Path.GetFileName(gamesPath);

        if (directory != null)
            try
            {
                InitializeWatcherAsync(directory, fileName);
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

    public async Task<IReadOnlyList<GameModel?>> LoadGamesAsync()
    {
        try
        {
            await _cacheLock.WaitAsync();
            try
            {
                if (_cachedGames != null) return _cachedGames;

                var gamesPath = AppPaths.GamesFilePath;

                if (!File.Exists(gamesPath))
                {
                    _logger.LogWarning("Games configuration file not found at {Path}", gamesPath);
                    return [];
                }

                var json = await File.ReadAllTextAsync(gamesPath);
                var games = JsonSerializer.Deserialize<List<GameModel?>>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (games == null)
                {
                    _logger.LogError("Failed to deserialize games from {Path}", gamesPath);
                    return [];
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
            return [];
        }
    }

    public async Task<GameModel?> GetGameByNameAsync(string? gameName)
    {
        if (string.IsNullOrWhiteSpace(gameName))
            throw new ArgumentException("Game name cannot be null or whitespace", nameof(gameName));

        var games = await LoadGamesAsync();
        return games.FirstOrDefault(g =>
            g is { GameName: not null } && g.GameName.Equals(gameName, StringComparison.OrdinalIgnoreCase));
    }

    public async Task SaveGamesAsync(IEnumerable<GameModel?>? games)
    {
        if (games == null)
        {
            _logger.LogWarning("Attempted to save null games collection");
            return;
        }

        await _cacheLock.WaitAsync();
        try
        {
            _isSaving = true;
            var gamesPath = AppPaths.GamesFilePath;
            var gamesList = games.ToList();
            var json = JsonSerializer.Serialize(gamesList, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(gamesPath, json);
            _cachedGames = gamesList.AsReadOnly();
        }
        finally
        {
            _isSaving = false;
            _cacheLock.Release();
        }
    }

    public bool IsGameRunning(GameModel? game)
    {
        if (game == null)
            throw new ArgumentNullException(nameof(game));

        if (string.IsNullOrWhiteSpace(game.GameExe))
        {
            _logger.LogWarning("Game executable path is empty for game {GameName}", game.GameName);
            return false;
        }

        try
        {
            var processes = Process.GetProcesses();
            var gameExeName = Path.GetFileNameWithoutExtension(game.GameExe);

            if (string.IsNullOrWhiteSpace(gameExeName))
            {
                _logger.LogWarning("Invalid executable name for game {GameName}: {ExePath}", game.GameName,
                    game.GameExe);
                return false;
            }

            var isMainExeRunning = processes.Any(p =>
            {
                try
                {
                    var isMatch = p.ProcessName.Equals(gameExeName, StringComparison.OrdinalIgnoreCase);
                    return isMatch;
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
                if (!string.IsNullOrWhiteSpace(altExeName))
                {
                    var isAltExeRunning = processes.Any(p =>
                    {
                        try
                        {
                            var isMatch = p.ProcessName.Equals(altExeName, StringComparison.OrdinalIgnoreCase);
                            return isMatch;
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Error checking process {ProcessName}", p.ProcessName);
                            return false;
                        }
                    });

                    if (isAltExeRunning) return true;
                }
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking if game is running for {GameName}", game.GameName);
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

    private void InitializeWatcherAsync(string directory, string fileName)
    {
        if (!Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
            _logger.LogInformation("Created directory for games config: {Path}", directory);
        }

        _watcher = new FileSystemWatcher(directory)
        {
            Filter = fileName,
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.CreationTime | NotifyFilters.Size,
            EnableRaisingEvents = true
        };

        _watcher.Changed += async (_, args) =>
        {
            if (args.ChangeType == WatcherChangeTypes.Changed && !_isSaving)
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
}