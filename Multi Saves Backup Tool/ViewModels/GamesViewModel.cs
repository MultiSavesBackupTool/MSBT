using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using FluentAvalonia.UI.Controls;
using Multi_Saves_Backup_Tool.Models;
using Multi_Saves_Backup_Tool.Services;
using Properties;

namespace Multi_Saves_Backup_Tool.ViewModels;

public class GamesViewModel : ViewModelBase
{
    private readonly IBackupService _backupService;
    private readonly IGamesService _gamesService;
    private ObservableCollection<GameModel> _games;

    public GamesViewModel(IGamesService gamesService, IBackupService backupService)
    {
        _gamesService = gamesService;
        _backupService = backupService;
        _games = new ObservableCollection<GameModel>();
        DeleteGameCommand = new AsyncRelayCommand<GameModel?>(DeleteGameAsync);
        EditGameCommand = new RelayCommand<GameModel?>(EditGame);
        _ = LoadGames();
    }

    public ObservableCollection<GameModel> Games
    {
        get => _games;
        set
        {
            if (_games != null)
                _games.CollectionChanged -= Games_CollectionChanged;
            SetProperty(ref _games, value);
            if (_games != null)
                _games.CollectionChanged += Games_CollectionChanged;
        }
    }

    public ICommand DeleteGameCommand { get; }
    public ICommand EditGameCommand { get; }

    public event EventHandler<GameModel>? EditGameRequested;

    private async void Games_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        await SaveGames();
        if (e.OldItems != null)
            foreach (GameModel item in e.OldItems)
                item.PropertyChanged -= Game_PropertyChanged;
        if (e.NewItems != null)
            foreach (GameModel item in e.NewItems)
                item.PropertyChanged += Game_PropertyChanged;
    }

    private async Task LoadGames()
    {
        var gamesList = await _gamesService.LoadGamesAsync();
        Games = new ObservableCollection<GameModel>(gamesList);
        foreach (var game in Games)
        {
            game.PropertyChanged += Game_PropertyChanged;
            UpdateBackupCount(game);
        }
    }

    private async void Game_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(GameModel.IsEnabled)) await SaveGames();
    }

    private async Task SaveGames()
    {
        await _gamesService.SaveGamesAsync(Games);
    }

    public void AddGame(GameModel game)
    {
        Games.Add(game);
        UpdateBackupCount(game);
    }

    public void UpdateGame(GameModel originalGame, GameModel updatedGame)
    {
        var index = Games.IndexOf(originalGame);
        if (index >= 0)
        {
            Games[index] = updatedGame;
            UpdateBackupCount(updatedGame);
        }
    }

    private void EditGame(GameModel? game)
    {
        if (game != null) EditGameRequested?.Invoke(this, game);
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

        if (result == ContentDialogResult.Primary) Games.Remove(game);
    }

    public void UpdateBackupCount(GameModel game)
    {
        try
        {
            game.BackupCount = _backupService.GetBackupCount(game);
        }
        catch (Exception e)
        {
            Console.WriteLine($"Error updating backup count: {e.Message}");
            game.BackupCount = 0;
        }
    }
}