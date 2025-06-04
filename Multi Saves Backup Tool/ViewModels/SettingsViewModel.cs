using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Multi_Saves_Backup_Tool.Models;

namespace Multi_Saves_Backup_Tool.ViewModels;

public partial class SettingsViewModel : ViewModelBase
{
    private readonly string _settingsPath;
    private readonly IStorageProvider? _storageProvider;

    [ObservableProperty]
    private ServiceSettings _settings;

    public SettingsViewModel()
    {
        _settingsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.json");
        _settings = LoadSettings();
    }

    public SettingsViewModel(IStorageProvider storageProvider) : this()
    {
        _storageProvider = storageProvider;
    }

    [RelayCommand]
    private async Task BrowseBackupFolder()
    {
        if (_storageProvider == null) return;

        var folder = await _storageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Select backup folder",
            AllowMultiple = false
        });

        if (folder.Count > 0)
        {
            Settings.BackupSettings.BackupRootFolder = folder[0].Path.LocalPath;
            await SaveCurrentSettingsAsync();
        }
    }

    private ServiceSettings LoadSettings()
    {
        try
        {
            if (!File.Exists(_settingsPath))
            {
                var defaultSettings = new ServiceSettings();
                SaveSettings(defaultSettings);
                return defaultSettings;
            }

            var json = File.ReadAllText(_settingsPath);
            var settings = JsonSerializer.Deserialize<ServiceSettings>(json, new JsonSerializerOptions());
            return settings ?? new ServiceSettings();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading settings: {ex}");
            return new ServiceSettings();
        }
    }

    private void SaveSettings(ServiceSettings settings)
    {
        try
        {
            var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions
            {
                WriteIndented = true
            });
            File.WriteAllText(_settingsPath, json);
            Settings = settings;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error saving settings: {ex}");
        }
    }

    [RelayCommand]
    private Task SaveCurrentSettingsAsync()
    {
        SaveSettings(Settings);
        return Task.CompletedTask;
    }
}
