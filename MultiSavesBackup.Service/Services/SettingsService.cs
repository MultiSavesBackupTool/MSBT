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

        // Определяем путь к папке с основным приложением
        var mainAppDirectory = Path.GetFullPath(Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "..",
            "..",
            "..",
            "Multi Saves Backup Tool",
            "bin",
            "Debug",
            "net9.0"));

        _settingsPath = Path.Combine(mainAppDirectory, "settings.json");
        _logger.LogInformation("Using settings file from main application: {Path}", _settingsPath);

        // Настраиваем отслеживание изменений файла настроек
        var settingsDir = Path.GetDirectoryName(_settingsPath);
        if (settingsDir != null)
        {
            _watcher = new FileSystemWatcher(settingsDir)
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
        else
        {
            _logger.LogError("Settings directory path is invalid");
            throw new DirectoryNotFoundException("Settings directory not found");
        }
    }

    public async Task SaveSettingsAsync(ServiceSettings settings)
    {
        try
        {
            var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions 
            { 
                WriteIndented = true 
            });
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
            // Добавляем небольшую задержку, чтобы избежать проблем с блокировкой файла
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
