using System;
using System.Threading.Tasks;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Linq;
using System.Text.Json;
using System.IO;
using System.Collections.Generic;
using Multi_Saves_Backup_Tool.Models;

namespace Multi_Saves_Backup_Tool.ViewModels;

public partial class AddGameOverlayViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _gameName = string.Empty;

    [ObservableProperty]
    private string _gameExe = string.Empty;

    [ObservableProperty]
    private string _gameExeAlt = string.Empty;

    [ObservableProperty]
    private string _saveLocation = string.Empty;

    [ObservableProperty]
    private string _modPath = string.Empty;

    [ObservableProperty]
    private string _addPath = string.Empty;

    [ObservableProperty]
    private int _daysForKeep;

    [ObservableProperty]
    private int _oldFilesStatus;

    [ObservableProperty]
    private bool _includeTimestamp = true;

    [ObservableProperty]
    private int _backupMode;

    [ObservableProperty]
    private string _gameNameError = string.Empty;

    [ObservableProperty]
    private string _gameExeError = string.Empty;

    [ObservableProperty]
    private string _saveLocationError = string.Empty;

    public event EventHandler? CloseRequested;

    [RelayCommand]
    private async Task BrowseSaveLocation(IStorageProvider storageProvider)
    {
        var folderPath = await BrowseFolder(storageProvider);
        if (!string.IsNullOrEmpty(folderPath))
            SaveLocation = folderPath;
    }

    [RelayCommand]
    private async Task BrowseModPath(IStorageProvider storageProvider)
    {
        var folderPath = await BrowseFolder(storageProvider);
        if (!string.IsNullOrEmpty(folderPath))
            ModPath = folderPath;
    }

    [RelayCommand]
    private async Task BrowseAddPath(IStorageProvider storageProvider)
    {
        var folderPath = await BrowseFolder(storageProvider);
        if (!string.IsNullOrEmpty(folderPath))
            AddPath = folderPath;
    }

    [RelayCommand]
    private async Task BrowseGameExe(IStorageProvider storageProvider)
    {
        var filePath = await BrowseExecutableFile(storageProvider);
        if (!string.IsNullOrEmpty(filePath))
            GameExe = filePath;
    }

    [RelayCommand]
    private async Task BrowseGameExeAlt(IStorageProvider storageProvider)
    {
        var filePath = await BrowseExecutableFile(storageProvider);
        if (!string.IsNullOrEmpty(filePath))
            GameExeAlt = filePath;
    }

    private void ClearErrors()
    {
        GameNameError = string.Empty;
        GameExeError = string.Empty;
        SaveLocationError = string.Empty;
    }

    private bool ValidateForm()
    {
        ClearErrors();
        var isValid = true;
        
        if (string.IsNullOrWhiteSpace(GameName))
        {
            GameNameError = "Game name is required";
            isValid = false;
        }
        
        if (string.IsNullOrWhiteSpace(GameExe))
        {
            GameExeError = "Game executable path is required";
            isValid = false;
        }
        
        if (string.IsNullOrWhiteSpace(SaveLocation))
        {
            SaveLocationError = "Save location is required";
            isValid = false;
        }

        return isValid;
    }

    [RelayCommand]
    private void Add()
    {
        if (!ValidateForm())
        {
            return;
        }

        var game = new GameModel
        {
            GameName = GameName,
            GameExe = GameExe,
            GameExeAlt = string.IsNullOrEmpty(GameExeAlt) ? null : GameExeAlt,
            SavePath = SaveLocation,
            ModPath = string.IsNullOrEmpty(ModPath) ? null : ModPath,
            AddPath = string.IsNullOrEmpty(AddPath) ? null : AddPath,
            DaysForKeep = DaysForKeep,
            SetOldFilesStatus = OldFilesStatus
        };

        var gamesFilePath = Path.Combine(Directory.GetCurrentDirectory(), "games.json");
        List<GameModel> games;

        if (File.Exists(gamesFilePath))
        {
            var json = File.ReadAllText(gamesFilePath);
            games = JsonSerializer.Deserialize<List<GameModel>>(json) ?? new List<GameModel>();
        }
        else
        {
            games = new List<GameModel>();
        }

        games.Add(game);
        var options = new JsonSerializerOptions { WriteIndented = true };
        File.WriteAllText(gamesFilePath, JsonSerializer.Serialize(games, options));

        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    private async Task<string> BrowseFolder(IStorageProvider storageProvider)
    {
        if (storageProvider != null)
        {
            var folder = await storageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = "Select Folder",
                AllowMultiple = false
            });

            return folder.Count > 0 ? folder[0].Path.LocalPath : string.Empty;
        }
        return string.Empty;
    }

    private async Task<string> BrowseExecutableFile(IStorageProvider storageProvider)
    {
        if (storageProvider != null)
        {
            var options = new FilePickerOpenOptions
            {
                Title = "Select Game Executable",
                AllowMultiple = false,
                FileTypeFilter = new[] 
                { 
                    new FilePickerFileType("Executable Files")
                    {
                        Patterns = new[] { "*.exe" },
                        MimeTypes = new[] { "application/x-msdownload" }
                    }
                }
            };

            var files = await storageProvider.OpenFilePickerAsync(options);
            return files.Count > 0 ? files[0].Path.LocalPath : string.Empty;
        }
        return string.Empty;
    }

    public void ClearForm()
    {
        GameName = string.Empty;
        GameExe = string.Empty;
        GameExeAlt = string.Empty;
        SaveLocation = string.Empty;
        ModPath = string.Empty;
        AddPath = string.Empty;
        DaysForKeep = 0;
        OldFilesStatus = 0;
        IncludeTimestamp = true;
        BackupMode = 0;
    }
}
