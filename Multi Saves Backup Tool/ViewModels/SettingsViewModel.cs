using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Multi_Saves_Backup_Tool.Models;
using Multi_Saves_Backup_Tool.Paths;
using Multi_Saves_Backup_Tool.Services.GameDiscovery;
using Properties;

namespace Multi_Saves_Backup_Tool.ViewModels;

public partial class SettingsViewModel : ViewModelBase
{
    private readonly IBlacklistService? _blacklistService;
    private readonly string _gamesPath;
    private readonly string _serviceStatePath;
    private readonly string _settingsPath;
    private readonly IStorageProvider? _storageProvider;
    private readonly IWhitelistService? _whitelistService;

    [ObservableProperty] private ServiceSettings _settings;

    private SettingsViewModel(ILogger<SettingsViewModel>? logger = null) : base(logger)
    {
        _settingsPath = AppPaths.SettingsFilePath;
        _gamesPath = AppPaths.GamesFilePath;
        _serviceStatePath = AppPaths.ServiceStateFilePath;
        _settings = LoadSettings();
    }

    public SettingsViewModel(IStorageProvider storageProvider, ILogger<SettingsViewModel>? logger = null) : this(logger)
    {
        _storageProvider = storageProvider;
    }

    public SettingsViewModel(IStorageProvider storageProvider, IBlacklistService blacklistService,
        IWhitelistService whitelistService, ILogger<SettingsViewModel>? logger = null) : this(logger)
    {
        _storageProvider = storageProvider;
        _blacklistService = blacklistService;
        _whitelistService = whitelistService;
    }

    public string CurrentVersion =>
        "Version: " + Assembly.GetExecutingAssembly().GetName().Version;

    public string BackupRootFolder
    {
        get => Settings.BackupSettings.BackupRootFolder;
        set
        {
            if (Settings.BackupSettings.BackupRootFolder != value)
            {
                Settings.BackupSettings.BackupRootFolder = value;
                OnPropertyChanged();
            }
        }
    }

    [RelayCommand]
    private async Task BrowseBackupFolder()
    {
        if (_storageProvider == null) return;

        var folder = await _storageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = Resources.BrowseBackupFolderTitle,
            AllowMultiple = false
        });

        if (folder.Count > 0)
        {
            BackupRootFolder = folder[0].Path.LocalPath;
            await SaveCurrentSettingsAsync();
        }
    }

    [RelayCommand]
    private async Task ExportSettings()
    {
        if (_storageProvider == null) return;

        try
        {
            var file = await _storageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = Resources.ExportTitle,
                DefaultExtension = "zip",
                SuggestedFileName = $"backup_settings_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.zip",
                FileTypeChoices =
                [
                    new FilePickerFileType("ZIP Archive")
                    {
                        Patterns = ["*.zip"]
                    }
                ]
            });

            if (file != null)
            {
                await using var stream = await file.OpenWriteAsync();
                using var archive = new ZipArchive(stream, ZipArchiveMode.Create);

                if (File.Exists(_settingsPath))
                {
                    var settingsEntry = archive.CreateEntry("settings.json");
                    await using var settingsStream = settingsEntry.Open();
                    await using var settingsFile = File.OpenRead(_settingsPath);
                    await settingsFile.CopyToAsync(settingsStream);
                }

                if (File.Exists(_gamesPath))
                {
                    var gamesEntry = archive.CreateEntry("games.json");
                    await using var gamesStream = gamesEntry.Open();
                    await using var gamesFile = File.OpenRead(_gamesPath);
                    await gamesFile.CopyToAsync(gamesStream);
                }

                if (File.Exists(_serviceStatePath))
                {
                    var serviceStateEntry = archive.CreateEntry("service_state.json");
                    await using var serviceStateStream = serviceStateEntry.Open();
                    await using var serviceStateFile = File.OpenRead(_serviceStatePath);
                    await serviceStateFile.CopyToAsync(serviceStateStream);
                }

                Debug.WriteLine("Settings exported successfully");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error exporting settings: {ex}");
        }
    }

    [RelayCommand]
    private async Task ImportSettings()
    {
        if (_storageProvider == null) return;

        try
        {
            var files = await _storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = Resources.ImportTitle,
                AllowMultiple = false,
                FileTypeFilter =
                [
                    new FilePickerFileType("ZIP Archive")
                    {
                        Patterns = ["*.zip"]
                    }
                ]
            });

            if (files.Count > 0)
            {
                await using var stream = await files[0].OpenReadAsync();
                using var archive = new ZipArchive(stream, ZipArchiveMode.Read);

                var importedFiles = 0;

                foreach (var entry in archive.Entries)
                {
                    var targetPath = entry.Name switch
                    {
                        "settings.json" => _settingsPath,
                        "games.json" => _gamesPath,
                        "service_state.json" => _serviceStatePath,
                        _ => null
                    };

                    if (targetPath != null)
                    {
                        await using var entryStream = entry.Open();
                        var buffer = new byte[entry.Length];
                        await entryStream.ReadExactlyAsync(buffer);
                        await File.WriteAllBytesAsync(targetPath, buffer);

                        importedFiles++;
                    }
                }

                if (importedFiles > 0)
                {
                    Settings = LoadSettings();
                    OnPropertyChanged(nameof(Settings));
                    OnPropertyChanged(nameof(BackupRootFolder));

                    Debug.WriteLine($"Settings imported successfully. {importedFiles} files restored.");
                }
                else
                {
                    Debug.WriteLine("No valid configuration files found in the archive.");
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error importing settings: {ex}");
        }
    }

    [RelayCommand]
    private async Task SyncBlacklist()
    {
        if (_blacklistService == null) return;

        try
        {
            await _blacklistService.SyncWithServerAsync();
        }
        catch
        {
            // Silently handle sync errors
        }
    }

    [RelayCommand]
    private async Task SyncWhitelist()
    {
        if (_whitelistService == null) return;

        try
        {
            await _whitelistService.SyncWithServerAsync();
        }
        catch
        {
            // Silently handle sync errors
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
            Debug.WriteLine($"Error loading settings: {ex}");
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
            Debug.WriteLine($"Error saving settings: {ex}");
        }
    }

    [RelayCommand]
    private Task SaveCurrentSettingsAsync()
    {
        SaveSettings(Settings);
        return Task.CompletedTask;
    }
}