using Microsoft.Extensions.Options;
using MultiSavesBackup.Service.Models;
using System.Text.Json;

namespace MultiSavesBackup.Service.Services;

public interface ISettingsService
{
    ServiceSettings CurrentSettings { get; }
    Task SaveSettingsAsync(ServiceSettings settings);
    Task ReloadSettingsAsync();
}

public class SettingsService : ISettingsService, IDisposable
{
    private readonly string _settingsPath;
    private ServiceSettings _currentSettings;
    private readonly ILogger<SettingsService> _logger;
    private readonly FileSystemWatcher? _watcher;
    private readonly SemaphoreSlim _settingsLock = new(1, 1);
    private DateTime _lastSettingsUpdate = DateTime.MinValue;
    private static readonly TimeSpan SettingsExpiration = TimeSpan.FromMinutes(5);

    public ServiceSettings CurrentSettings => _currentSettings;

    public SettingsService(IOptions<ServiceSettings> options, ILogger<SettingsService> logger)
    {
        _currentSettings = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        var mainAppDirectory = AppContext.BaseDirectory;
        _settingsPath = Path.Combine(mainAppDirectory, "settings.json");

        if (_currentSettings.BackupSettings == null)
        {
            _currentSettings.BackupSettings = new BackupSettings
            {
                GamesConfigPath = Path.Combine(mainAppDirectory, "games.json")
            };
        }
        else if (string.IsNullOrEmpty(_currentSettings.BackupSettings.GamesConfigPath))
        {
            _currentSettings.BackupSettings.GamesConfigPath = Path.Combine(mainAppDirectory, "games.json");
        }

        _logger.LogInformation("Using settings file: {Path}", _settingsPath);
        _logger.LogInformation("Using games config: {Path}", _currentSettings.BackupSettings.GamesConfigPath);

        try
        {
            _watcher = new FileSystemWatcher(mainAppDirectory)
            {
                Filter = "settings.json",
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.CreationTime | NotifyFilters.Size,
                EnableRaisingEvents = true
            };

            _watcher.Changed += async (_, args) =>
            {
                if (args.ChangeType == WatcherChangeTypes.Changed)
                {
                    _logger.LogInformation("Settings file changed, reloading settings");
                    await ReloadSettingsAsync();
                }
            };

            _watcher.Created += async (_, _) =>
            {
                _logger.LogInformation("Settings file created, reloading settings");
                await ReloadSettingsAsync();
            };

            _watcher.Deleted += async (_, _) =>
            {
                _logger.LogInformation("Settings file deleted, using defaults");
                await ReloadSettingsAsync();
            };

            _watcher.Error += (_, ex) =>
            {
                _logger.LogError(ex, "Error in FileSystemWatcher for settings");
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize FileSystemWatcher for settings");
        }
    }

    public async Task SaveSettingsAsync(ServiceSettings settings)
    {
        if (settings == null)
            throw new ArgumentNullException(nameof(settings));

        await _settingsLock.WaitAsync();
        try
        {
            var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(_settingsPath, json);
            _currentSettings = settings;
            _lastSettingsUpdate = DateTime.Now;
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
            await Task.Delay(100); // Wait for file to be fully written

            if (!File.Exists(_settingsPath))
            {
                _logger.LogWarning("Settings file not found at {Path}, using defaults", _settingsPath);
                _currentSettings = new ServiceSettings();
                _lastSettingsUpdate = DateTime.Now;
                return;
            }

            var json = await File.ReadAllTextAsync(_settingsPath);
            var newSettings = JsonSerializer.Deserialize<ServiceSettings>(json) 
                ?? throw new InvalidOperationException("Failed to deserialize settings");
            
            _currentSettings = newSettings;
            _lastSettingsUpdate = DateTime.Now;
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

    public void Dispose()
    {
        _watcher?.Dispose();
        _settingsLock.Dispose();
    }
}
