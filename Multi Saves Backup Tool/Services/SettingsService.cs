using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Multi_Saves_Backup_Tool.Models;
using Multi_Saves_Backup_Tool.Paths;

namespace Multi_Saves_Backup_Tool.Services;

public interface ISettingsService
{
    ServiceSettings CurrentSettings { get; }
    Task SaveSettingsAsync(ServiceSettings settings);
    Task ReloadSettingsAsync();
}

public class SettingsService : ISettingsService, IDisposable
{
    private readonly ILogger<SettingsService> _logger;
    private readonly SemaphoreSlim _settingsLock = new(1, 1);
    private readonly string _settingsPath;
    private readonly FileSystemWatcher? _watcher;

    public SettingsService(IOptions<ServiceSettings> options, ILogger<SettingsService> logger)
    {
        CurrentSettings = options.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _settingsPath = AppPaths.SettingsFilePath;

        if (string.IsNullOrEmpty(CurrentSettings.BackupSettings.GamesConfigPath))
            CurrentSettings.BackupSettings.GamesConfigPath = AppPaths.GamesFilePath;

        _logger.LogInformation("Using settings file: {Path}", _settingsPath);
        _logger.LogInformation("Using games config: {Path}", CurrentSettings.BackupSettings.GamesConfigPath);

        try
        {
            var directory = Path.GetDirectoryName(_settingsPath);
            if (directory == null)
            {
                _logger.LogError("Could not get directory for settings file: {Path}", _settingsPath);
                return;
            }

            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
                _logger.LogInformation("Created directory for settings file: {Path}", directory);
            }

            _watcher = new FileSystemWatcher(directory)
            {
                Filter = "settings.json",
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.CreationTime | NotifyFilters.Size,
                EnableRaisingEvents = true
            };

            _watcher.Changed += async (_, args) =>
            {
                if (args.ChangeType == WatcherChangeTypes.Changed)
                {
                    _logger.LogInformation("Settings file changed, reloading immediately");
                    await ReloadSettingsAsync();
                }
            };

            _watcher.Created += async (_, _) =>
            {
                _logger.LogInformation("Settings file created, reloading immediately");
                await ReloadSettingsAsync();
            };

            _watcher.Deleted += async (_, _) =>
            {
                _logger.LogInformation("Settings file deleted, using defaults");
                await ReloadSettingsAsync();
            };

            _watcher.Error += (_, ex) =>
            {
                _logger.LogError("Error in FileSystemWatcher for settings: {Message}", ex.GetException().Message);
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize FileSystemWatcher for settings");
        }
    }

    public void Dispose()
    {
        _watcher?.Dispose();
        _settingsLock.Dispose();
    }

    public ServiceSettings CurrentSettings { get; private set; }

    public async Task SaveSettingsAsync(ServiceSettings settings)
    {
        if (settings == null)
            throw new ArgumentNullException(nameof(settings));

        await _settingsLock.WaitAsync();
        try
        {
            var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(_settingsPath, json);
            CurrentSettings = settings;
            _logger.LogInformation("Settings successfully saved to {Path}", _settingsPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save settings to {Path}", _settingsPath);
            throw;
        }
        finally
        {
            _settingsLock.Release();
        }
    }

    public async Task ReloadSettingsAsync()
    {
        await _settingsLock.WaitAsync();
        try
        {
            await Task.Delay(100);

            if (!File.Exists(_settingsPath))
            {
                _logger.LogWarning("Settings file not found at {Path}, using defaults", _settingsPath);
                CurrentSettings = new ServiceSettings();
                return;
            }

            var json = await File.ReadAllTextAsync(_settingsPath);
            var newSettings = JsonSerializer.Deserialize<ServiceSettings>(json)
                              ?? throw new InvalidOperationException("Failed to deserialize settings");

            CurrentSettings = newSettings;
            _logger.LogInformation("Settings successfully reloaded from {Path}", _settingsPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to reload settings from {Path}", _settingsPath);
            throw;
        }
        finally
        {
            _settingsLock.Release();
        }
    }
}