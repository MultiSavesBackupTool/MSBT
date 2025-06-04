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
    private readonly FileSystemWatcher _watcher;

    public ServiceSettings CurrentSettings => _currentSettings;

    public SettingsService(IOptions<ServiceSettings> options, ILogger<SettingsService> logger)
    {
        _currentSettings = options.Value;
        _logger = logger;

        var gamesConfigPath = _currentSettings.BackupSettings.GamesConfigPath;
        var mainAppDirectory = Path.GetDirectoryName(gamesConfigPath);
        
        if (string.IsNullOrEmpty(mainAppDirectory))
        {
            throw new InvalidOperationException("Cannot determine main application directory from GamesConfigPath");
        }

        _settingsPath = Path.Combine(mainAppDirectory, "settings.json");
        _logger.LogInformation("Using settings file: {Path}", _settingsPath);

        _watcher = new FileSystemWatcher(mainAppDirectory)
        {
            Filter = "settings.json",
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.CreationTime,
            EnableRaisingEvents = true
        };

        _watcher.Changed += async (sender, args) =>
        {
            if (args.ChangeType == WatcherChangeTypes.Changed)
            {
                await ReloadSettingsAsync();
            }
        };
    }

    public async Task SaveSettingsAsync(ServiceSettings settings)
    {
        try
        {
            var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(_settingsPath, json);
            _currentSettings = settings;
            _logger.LogInformation("Settings successfully saved to {Path}", _settingsPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save settings to {Path}", _settingsPath);
            throw;
        }
    }

    public async Task ReloadSettingsAsync()
    {
        try
        {
            await Task.Delay(100);

            if (!File.Exists(_settingsPath))
            {
                _logger.LogWarning("Settings file not found at {Path}, using defaults", _settingsPath);
                return;
            }

            var json = await File.ReadAllTextAsync(_settingsPath);
            var newSettings = JsonSerializer.Deserialize<ServiceSettings>(json) 
                ?? throw new InvalidOperationException("Failed to deserialize settings");
            
            _currentSettings = newSettings;
            _logger.LogInformation("Settings successfully reloaded from {Path}", _settingsPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to reload settings from {Path}", _settingsPath);
            throw;
        }
    }

    public void Dispose()
    {
        _watcher?.Dispose();
    }
}
