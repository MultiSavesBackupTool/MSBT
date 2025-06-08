using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using FluentAvalonia.UI.Controls;
using Multi_Saves_Backup_Tool.Models;
using Properties;

namespace Multi_Saves_Backup_Tool.ViewModels;

public class GamesViewModel : ViewModelBase
{
    private ObservableCollection<GameModel> _games;

    public GamesViewModel()
    {
        _games = new ObservableCollection<GameModel>();
        DeleteGameCommand = new AsyncRelayCommand<GameModel?>(DeleteGameAsync);
        EditGameCommand = new RelayCommand<GameModel?>(EditGame);
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
    public ICommand EditGameCommand { get; }

    public event EventHandler<GameModel>? EditGameRequested;

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
                List<GameModel>? gamesList = null;
            
                try
                {
                    var gamesDict = JsonSerializer.Deserialize<Dictionary<string, GameModel>>(json, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
                    if (gamesDict != null)
                    {
                        gamesList = gamesDict.Values.ToList();
                    }
                }
                catch
                {
                    try
                    {
                        var gamesArray = JsonSerializer.Deserialize<List<GameModel>>(json, new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        });
                        if (gamesArray != null)
                        {
                            gamesList = gamesArray;
                            Games = new ObservableCollection<GameModel>(gamesList);
                            foreach (var game in Games)
                                game.PropertyChanged += Game_PropertyChanged;
                            SaveGames();
                            return;
                        }
                    }
                    catch
                    {
                        Games = new ObservableCollection<GameModel>();
                        return;
                    }
                }

                if (gamesList != null)
                {
                    Games = new ObservableCollection<GameModel>(gamesList);
                    foreach (var game in Games)
                        game.PropertyChanged += Game_PropertyChanged;
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
        
            var gamesDict = Games.ToDictionary(game => game.GameName, game => game);
        
            var json = JsonSerializer.Serialize(gamesDict, new JsonSerializerOptions
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

    public void UpdateGame(GameModel originalGame, GameModel updatedGame)
    {
        var index = Games.IndexOf(originalGame);
        if (index >= 0)
        {
            originalGame.PropertyChanged -= Game_PropertyChanged;
            
            Games[index] = updatedGame;
            
            updatedGame.PropertyChanged += Game_PropertyChanged;
            
            SaveGames();
        }
    }

    private void EditGame(GameModel? game)
    {
        if (game != null)
        {
            EditGameRequested?.Invoke(this, game);
        }
    }

    private async Task DeleteGameAsync(GameModel? game)
    {
        if (game is null) return;

        var dialog = new ContentDialog
        {
            Title = Resources.DeleteConfirmation_Title,
            Content = string.Format(Resources.DeleteConfirmation_Message, game.GameName),
            PrimaryButtonText = Resources.DeleteConfirmation_Delete,
            SecondaryButtonText = Resources.DeleteConfirmation_Cancel,
            DefaultButton = ContentDialogButton.Secondary
        };

        var result = await dialog.ShowAsync();

        if (result == ContentDialogResult.Primary)
        {
            game.PropertyChanged -= Game_PropertyChanged;
            Games.Remove(game);
            SaveGames();
        }
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