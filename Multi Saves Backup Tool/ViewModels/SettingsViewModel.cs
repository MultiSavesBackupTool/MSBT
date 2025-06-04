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

    public override string Title => "Настройки";

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
            Title = "Выберите папку для резервных копий",
            AllowMultiple = false
        });

        if (folder.Count > 0)
        {
            Settings.BackupSettings.BackupRootFolder = folder[0].Path.LocalPath;
            await SaveCurrentSettingsAsync();
        }
    }

    [RelayCommand]
    private async Task BrowseLogFile()
    {
        if (_storageProvider == null) return;

        var file = await _storageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Выберите расположение файла лога",
            DefaultExtension = "log",
            ShowOverwritePrompt = true,
            SuggestedFileName = "backup_service.log"
        });

        if (file != null)
        {
            Settings.BackupSettings.LogPath = file.Path.LocalPath;
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
            Console.WriteLine($"Ошибка при загрузке настроек: {ex}");
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
            Console.WriteLine($"Ошибка при сохранении настроек: {ex}");
        }
    }

    [RelayCommand]
    private Task SaveCurrentSettingsAsync()
    {
        SaveSettings(Settings);
        return Task.CompletedTask;
    }
}
