using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using FluentAvalonia.UI.Controls;
using Multi_Saves_Backup_Tool.Dependes;
using Multi_Saves_Backup_Tool.Models;
using Multi_Saves_Backup_Tool.Services;
using Multi_Saves_Backup_Tool.Services.GameDiscovery;
using Properties;

namespace Multi_Saves_Backup_Tool.ViewModels;

public class GamesViewModel : ViewModelBase
{
    private readonly IBackupService _backupService;
    private readonly IGamesService _gamesService;
    private readonly InstalledGamesScanner _installedGamesScanner = new();
    private ObservableCollection<GameModel> _games;

    [SupportedOSPlatform("windows")]
    public GamesViewModel(IGamesService gamesService, IBackupService backupService)
    {
        _gamesService = gamesService;
        _backupService = backupService;
        _games = new ObservableCollection<GameModel>();
        DeleteGameCommand = new AsyncRelayCommand<GameModel?>(DeleteGameAsync);
        EditGameCommand = new RelayCommand<GameModel?>(EditGame);
        OpenSaveCommand = new RelayCommand<GameModel?>(OpenSave);
        RestoreBackupCommand = new AsyncRelayCommand<GameModel?>(RestoreBackupAsync);
        ScanInstalledGamesCommand = new AsyncRelayCommand(ScanInstalledGamesAsync);
        _ = LoadGames();
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
    public ICommand OpenSaveCommand { get; }
    public ICommand RestoreBackupCommand { get; }
    public ICommand ScanInstalledGamesCommand { get; }

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

    private void OpenSave(GameModel? game)
    {
        if (game != null) FolderOpener.OpenFolder(game.SavePath);
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

    private async Task RestoreBackupAsync(GameModel? game)
    {
        if (game == null)
            return;

        try
        {
            await _backupService.RestoreLatestBackupAsync(game);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error restore backup: {ex.Message}");
        }
    }

    public void UpdateBackupCount(GameModel game)
    {
        try
        {
            game.BackupCount = _backupService.GetBackupCount(game);
        }
        catch (Exception e)
        {
            Debug.WriteLine($"Error updating backup count: {e.Message}");
            game.BackupCount = 0;
        }
    }

    [SupportedOSPlatform("windows")]
    private async Task ScanInstalledGamesAsync()
    {
        var foundGames = await _installedGamesScanner.ScanForInstalledGamesAsync();
        foreach (var game in foundGames)
            if (!Games.Any(g => g.GameName == game.GameName && g.GameExe == game.GameExe))
            {
                Games.Add(game);
                UpdateBackupCount(game);
            }

        await SaveGames();
    }
}