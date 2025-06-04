using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.IO;
using System.Text.Json;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using Multi_Saves_Backup_Tool.Models;

namespace Multi_Saves_Backup_Tool.ViewModels;

public class GamesViewModel : ViewModelBase
{
    private ObservableCollection<GameModel> _games;
    public ObservableCollection<GameModel> Games
    {
        get => _games;
        set
        {
            if (_games != null)
            {
                _games.CollectionChanged -= Games_CollectionChanged;
            }
            SetProperty(ref _games, value);
            if (_games != null)
            {
                _games.CollectionChanged += Games_CollectionChanged;
            }
        }
    }

    public ICommand DeleteGameCommand { get; }

    public GamesViewModel()
    {
        _games = new ObservableCollection<GameModel>();
        DeleteGameCommand = new RelayCommand<GameModel>(DeleteGame);
        LoadGames();
    }

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
                    foreach (var game in Games)
                    {
                        game.PropertyChanged += Game_PropertyChanged;
                    }
                }
            }
        }
        catch (Exception)
        {
            Games = new ObservableCollection<GameModel>();
        }
    }

    private void Game_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(GameModel.IsEnabled))
        {
            SaveGames();
        }
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
        catch (Exception)
        {
            // Можно добавить логирование ошибки
        }
    }

    private void DeleteGame(GameModel? game)
    {
        if (game != null)
        {
            game.PropertyChanged -= Game_PropertyChanged;
            Games.Remove(game);
            SaveGames();
        }
    }
}
