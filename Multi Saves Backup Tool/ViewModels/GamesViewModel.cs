using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using Multi_Saves_Backup_Tool.Models;

namespace Multi_Saves_Backup_Tool.ViewModels;

public class GamesViewModel : ViewModelBase
{
    private ObservableCollection<GameModel> _games;

    public GamesViewModel()
    {
        _games = new ObservableCollection<GameModel>();
        DeleteGameCommand = new RelayCommand<GameModel?>(DeleteGame);
        LoadGames();
    }

    public ObservableCollection<GameModel> Games
    {
        get => _games;
        set
        {
            _games.CollectionChanged -= Games_CollectionChanged;
            SetProperty(ref _games, value);
            _games.CollectionChanged += Games_CollectionChanged;
        }
    }

    public ICommand DeleteGameCommand { get; }

    private void Games_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        SaveGames();
    }

    private void LoadGames()
    {
        try
        {
            var jsonPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "games.json");
            if (File.Exists(jsonPath))
            {
                var json = File.ReadAllText(jsonPath);
                var games = JsonSerializer.Deserialize<List<GameModel>>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                if (games != null)
                {
                    Games = new ObservableCollection<GameModel>(games);
                    foreach (var game in Games) game.PropertyChanged += Game_PropertyChanged;
                }
            }
        }
        catch (Exception)
        {
            Games = new ObservableCollection<GameModel>();
        }
    }

    private void Game_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(GameModel.IsEnabled)) SaveGames();
    }

    private void SaveGames()
    {
        try
        {
            var jsonPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "games.json");
            var json = JsonSerializer.Serialize(Games, new JsonSerializerOptions
            {
                WriteIndented = true
            });
            File.WriteAllText(jsonPath, json);
        }
        catch (Exception e)
        {
            Console.WriteLine($"Error saving games: {e.Message}");
        }
    }

    public void AddGame(GameModel game)
    {
        Games.Add(game);
        game.PropertyChanged += Game_PropertyChanged;
    }

    private void DeleteGame(GameModel? game)
    {
        if (game is null) return;

        game.PropertyChanged -= Game_PropertyChanged;
        Games.Remove(game);
        SaveGames();
    }

    public void UpdateBackupCount(GameModel game)
    {
        try
        {
            var settings = new ServiceSettings();
            var backupDir = Path.Combine(settings.BackupSettings.BackupRootFolder, GetSafeDirectoryName(game.GameName));
            if (Directory.Exists(backupDir))
            {
                var count = Directory.GetFiles(backupDir, "*.zip").Length;
                game.BackupCount = count;
            }
            else
            {
                game.BackupCount = 0;
            }
        }
        catch (Exception e)
        {
            Console.WriteLine($"Error updating backup count: {e.Message}");
            game.BackupCount = 0;
        }
    }

    private string GetSafeDirectoryName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars().Concat(Path.GetInvalidPathChars()).ToArray();
        return string.Join("_", name.Split(invalid));
    }
}