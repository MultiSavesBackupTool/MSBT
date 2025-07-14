using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using FluentAvalonia.UI.Controls;
using Microsoft.Extensions.Logging;
using Multi_Saves_Backup_Tool.Dependes;
using Multi_Saves_Backup_Tool.Models;
using Multi_Saves_Backup_Tool.Services;
using Multi_Saves_Backup_Tool.Services.GameDiscovery;
using Properties;

namespace Multi_Saves_Backup_Tool.ViewModels;

public class GamesViewModel : ViewModelBase
{
    private readonly IBackupService _backupService;
    private readonly IBlacklistService _blacklistService;
    private readonly IGamesService _gamesService;
    private readonly InstalledGamesScanner _installedGamesScanner;
    private readonly IWhitelistService _whitelistService;
    private ObservableCollection<GameModel>? _filteredGames;
    private ObservableCollection<GameModel>? _games;

    private bool _isLoading;
    private string _searchText = string.Empty;

    [SupportedOSPlatform("windows")]
    public GamesViewModel(IGamesService gamesService, IBackupService backupService, IBlacklistService blacklistService,
        IWhitelistService whitelistService, ILogger<GamesViewModel>? logger = null) : base(logger)
    {
        _gamesService = gamesService ?? throw new ArgumentNullException(nameof(gamesService));
        _backupService = backupService ?? throw new ArgumentNullException(nameof(backupService));
        _blacklistService = blacklistService ?? throw new ArgumentNullException(nameof(blacklistService));
        _whitelistService = whitelistService ?? throw new ArgumentNullException(nameof(whitelistService));

        DeleteGameCommand = new AsyncRelayCommand<GameModel>(DeleteGameAsync, CanDeleteGame);
        EditGameCommand = new Services.RelayCommand<GameModel>(EditGame, CanEditGame);
        OpenSaveCommand = new Services.RelayCommand<GameModel>(OpenSave, CanOpenSave);
        RestoreBackupCommand = new AsyncRelayCommand<GameModel>(RestoreBackupAsync, CanRestoreBackup);
        ScanInstalledGamesCommand = new AsyncRelayCommand(ScanInstalledGamesAsync, CanScanInstalledGames);
        AddToBlacklistCommand = new AsyncRelayCommand<GameModel>(AddToBlacklistAsync, CanAddToBlacklist);
        AddToWhitelistCommand = new AsyncRelayCommand<GameModel>(AddToWhitelistAsync, CanAddToWhitelist);

        _installedGamesScanner = new InstalledGamesScanner(_blacklistService, _whitelistService);

        _ = LoadGamesAsync();
    }

    public ObservableCollection<GameModel>? Games
    {
        get => _games;
        set
        {
            if (_games != value)
            {
                if (_games != null)
                    _games.CollectionChanged -= Games_CollectionChanged;

                _games = value;

                if (_games != null)
                    _games.CollectionChanged += Games_CollectionChanged;

                OnPropertyChanged();
                UpdateFilteredGames();
            }
        }
    }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (_searchText != value)
            {
                _searchText = value;
                OnPropertyChanged();
                UpdateFilteredGames();
            }
        }
    }

    public ObservableCollection<GameModel>? FilteredGames
    {
        get => _filteredGames;
        private set
        {
            if (_filteredGames != value)
            {
                _filteredGames = value;
                OnPropertyChanged();
            }
        }
    }

    public bool IsLoading
    {
        get => _isLoading;
        private set
        {
            if (_isLoading != value)
            {
                _isLoading = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsNotLoading));
            }
        }
    }

    public bool IsNotLoading => !IsLoading;

    public IAsyncRelayCommand DeleteGameCommand { get; }
    public ICommand EditGameCommand { get; }
    public ICommand OpenSaveCommand { get; }
    public IAsyncRelayCommand RestoreBackupCommand { get; }
    public IAsyncRelayCommand ScanInstalledGamesCommand { get; }
    public IAsyncRelayCommand<GameModel> AddToBlacklistCommand { get; }
    public IAsyncRelayCommand<GameModel> AddToWhitelistCommand { get; }

    public event EventHandler<GameModel>? EditGameRequested;

    private async void Games_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        try
        {
            await SaveGamesAsync();
            if (e.OldItems != null)
                foreach (GameModel item in e.OldItems)
                    if (item != null)
                        item.PropertyChanged -= Game_PropertyChanged;
            if (e.NewItems != null)
                foreach (GameModel item in e.NewItems)
                    if (item != null)
                        item.PropertyChanged += Game_PropertyChanged;
            UpdateFilteredGames();
        }
        catch (Exception ex)
        {
            LogError(ex, "Error in Games_CollectionChanged");
        }
    }

    private async Task LoadGamesAsync()
    {
        try
        {
            IsLoading = true;
            var gamesList = await _gamesService.LoadGamesAsync();
            Games = new ObservableCollection<GameModel>(gamesList!);
            foreach (var game in Games)
            {
                game.PropertyChanged += Game_PropertyChanged;
                UpdateBackupCount(game);
            }

            await RemoveBlacklistedGamesAsync();
        }
        catch (Exception ex)
        {
            LogError(ex, "Error loading games");
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async void Game_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(GameModel.IsEnabled))
            try
            {
                await SaveGamesAsync();
            }
            catch (Exception ex)
            {
                LogError(ex, "Error saving games after property change");
            }
    }

    private async Task SaveGamesAsync()
    {
        try
        {
            await _gamesService.SaveGamesAsync(Games);
        }
        catch (Exception ex)
        {
            LogError(ex, "Error saving games");
            throw;
        }
    }

    public void AddGame(GameModel game)
    {
        Games?.Add(game);
        UpdateBackupCount(game);
        UpdateFilteredGames();
    }

    public void UpdateGame(GameModel originalGame, GameModel updatedGame)
    {
        if (Games != null)
        {
            var index = Games.IndexOf(originalGame);
            if (index >= 0)
            {
                Games[index] = updatedGame;
                UpdateBackupCount(updatedGame);
                UpdateFilteredGames();
            }
        }
    }

    private void OpenSave(GameModel? game)
    {
        if (game?.SavePath != null && !string.IsNullOrWhiteSpace(game.SavePath))
            try
            {
                FolderOpener.OpenFolder(game.SavePath);
            }
            catch (Exception ex)
            {
                LogError(ex, "Error opening save folder for game {GameName}", game.GameName);
            }
    }

    private void EditGame(GameModel? game)
    {
        if (game != null) EditGameRequested?.Invoke(this, game);
    }

    private async Task DeleteGameAsync(GameModel? game)
    {
        if (game is null) return;

        try
        {
            var dialog = new ContentDialog
            {
                Title = Resources.DeleteConfirmation_Title,
                Content = string.Format(Resources.DeleteConfirmation_Message, game.GameName),
                PrimaryButtonText = Resources.DeleteConfirmation_Delete,
                SecondaryButtonText = Resources.DeleteConfirmation_Cancel,
                DefaultButton = ContentDialogButton.Secondary
            };

            var result = await dialog.ShowAsync();

            if (result == ContentDialogResult.Primary) Games?.Remove(game);
        }
        catch (Exception ex)
        {
            LogError(ex, "Error deleting game {GameName}", game.GameName);
        }
    }

    private async Task RestoreBackupAsync(GameModel? game)
    {
        if (game == null) return;

        try
        {
            await _backupService.RestoreLatestBackupAsync(game);
            LogInformation("Successfully restored backup for game {GameName}", game.GameName);
        }
        catch (Exception ex)
        {
            LogError(ex, "Error restoring backup for game {GameName}", game.GameName);
        }
    }

    public void UpdateBackupCount(GameModel game)
    {
        try
        {
            game.BackupCount = _backupService.GetBackupCount(game);
        }
        catch (Exception ex)
        {
            LogError(ex, "Error updating backup count for game {GameName}", game.GameName);
            game.BackupCount = 0;
        }
    }

    private void UpdateFilteredGames()
    {
        if (Games == null)
        {
            FilteredGames = null;
            return;
        }

        if (string.IsNullOrWhiteSpace(SearchText))
        {
            FilteredGames = new ObservableCollection<GameModel>(Games);
        }
        else
        {
            var lower = SearchText.ToLowerInvariant();
            FilteredGames = new ObservableCollection<GameModel>(
                Games.Where(g =>
                    (!string.IsNullOrEmpty(g.GameName) && g.GameName.ToLowerInvariant().Contains(lower)) ||
                    (!string.IsNullOrEmpty(g.SavePath) && g.SavePath.ToLowerInvariant().Contains(lower)) ||
                    (!string.IsNullOrEmpty(g.GameExe) && g.GameExe.ToLowerInvariant().Contains(lower))
                )
            );
        }
    }

    [SupportedOSPlatform("windows")]
    private async Task ScanInstalledGamesAsync()
    {
        try
        {
            IsLoading = true;
            var foundGames = await _installedGamesScanner.ScanForInstalledGamesAsync();
            var newGames = new List<GameModel>();

            foreach (var game in foundGames)
                if (Games != null && !Games.Any(g => g.GameName == game.GameName && g.GameExe == game.GameExe))
                {
                    UpdateBackupCount(game);
                    newGames.Add(game);
                }

            if (newGames.Count > 0)
            {
                foreach (var game in newGames) Games?.Add(game);

                await SaveGamesAsync();
                LogInformation("Added {Count} new games from scan", newGames.Count);
            }

            await RemoveBlacklistedGamesAsync();
        }
        catch (Exception ex)
        {
            LogError(ex, "Error scanning for installed games");
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task RemoveBlacklistedGamesAsync()
    {
        if (Games == null) return;

        try
        {
            var gamesToRemove = new List<GameModel>();

            foreach (var game in Games)
                if (game.GameName != null && _blacklistService.IsBlacklisted(game.GameName))
                    gamesToRemove.Add(game);

            if (gamesToRemove.Count > 0)
            {
                foreach (var game in gamesToRemove)
                {
                    Games.Remove(game);
                    LogInformation("Removed blacklisted game: {GameName}", game.GameName);
                }

                await SaveGamesAsync();
                LogInformation("Removed {Count} blacklisted games from the list", gamesToRemove.Count);
            }
        }
        catch (Exception ex)
        {
            LogError(ex, "Error removing blacklisted games");
        }
    }

    private async Task AddToBlacklistAsync(GameModel? game)
    {
        if (game == null) return;

        try
        {
            if (game.GameName != null)
            {
                await _blacklistService.AddToBlacklistAsync(game.GameName);
                await _blacklistService.ContributeToServerAsync(game.GameName);

                Games?.Remove(game);

                LogInformation("Added {GameName} to blacklist and contributed to server", game.GameName);
            }
        }
        catch (Exception ex)
        {
            LogError(ex, "Error adding {GameName} to blacklist", game.GameName);
        }
    }

    private async Task AddToWhitelistAsync(GameModel? game)
    {
        if (game == null) return;

        try
        {
            var whitelistEntry = WhitelistEntry.FromGameModel(game);
            await _whitelistService.AddToWhitelistAsync(whitelistEntry);
            await _whitelistService.ContributeToServerAsync(whitelistEntry);

            LogInformation("Added {GameName} to whitelist and contributed to server", game.GameName);
        }
        catch (Exception ex)
        {
            LogError(ex, "Error adding {GameName} to whitelist", game.GameName);
        }
    }

    private bool CanDeleteGame(GameModel? game)
    {
        return game != null && !IsLoading;
    }

    private bool CanEditGame(GameModel? game)
    {
        return game != null && !IsLoading;
    }

    private bool CanOpenSave(GameModel? game)
    {
        return game?.SavePath != null && !string.IsNullOrWhiteSpace(game.SavePath) && !IsLoading;
    }

    private bool CanRestoreBackup(GameModel? game)
    {
        return game != null && !IsLoading;
    }

    private bool CanScanInstalledGames()
    {
        return !IsLoading;
    }

    private bool CanAddToBlacklist(GameModel? game)
    {
        return game != null && !IsLoading;
    }

    private bool CanAddToWhitelist(GameModel? game)
    {
        return game != null && !IsLoading;
    }
}