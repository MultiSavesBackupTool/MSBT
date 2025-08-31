using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Input;
using FluentAvalonia.UI.Controls;
using Microsoft.Extensions.Logging;
using Multi_Saves_Backup_Tool.Dependes;
using Multi_Saves_Backup_Tool.Models;
using Multi_Saves_Backup_Tool.Services;
using Multi_Saves_Backup_Tool.Services.GameDiscovery;
using Properties;
using RelayCommand = Multi_Saves_Backup_Tool.Services.RelayCommand;

namespace Multi_Saves_Backup_Tool.ViewModels;

public class GamesViewModel : ViewModelBase
{
    private readonly BackupManager _backupManager;
    private readonly IBackupService _backupService;
    private readonly IBlacklistService _blacklistService;
    private readonly IGamesService _gamesService;
    private readonly InstalledGamesScanner _installedGamesScanner;
    private readonly INotificationService _notificationService;
    private readonly IWhitelistService _whitelistService;
    private ObservableCollection<GameModel>? _filteredGames;
    private ObservableCollection<GameModel>? _games;

    private bool _isLoading;
    private string _searchText = string.Empty;
    private bool _showHiddenGames;
    private bool _showOnlyDisabled;
    private bool _showOnlyEnabled;

    [SupportedOSPlatform("windows")]
    public GamesViewModel(IGamesService gamesService, IBackupService backupService, IBlacklistService blacklistService,
        IWhitelistService whitelistService, BackupManager backupManager,
        INotificationService notificationService,
        ILogger<GamesViewModel>? logger = null) : base(logger)
    {
        _gamesService = gamesService ?? throw new ArgumentNullException(nameof(gamesService));
        _backupService = backupService ?? throw new ArgumentNullException(nameof(backupService));
        _blacklistService = blacklistService ?? throw new ArgumentNullException(nameof(blacklistService));
        _whitelistService = whitelistService ?? throw new ArgumentNullException(nameof(whitelistService));
        _backupManager = backupManager ?? throw new ArgumentNullException(nameof(backupManager));
        _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));

        DeleteGameCommand = new AsyncRelayCommand<GameModel>(DeleteGameAsync, CanDeleteGame);
        EditGameCommand = new Services.RelayCommand<GameModel>(EditGame, CanEditGame);
        OpenSaveCommand = new Services.RelayCommand<GameModel>(OpenSave, CanOpenSave);
        RestoreBackupCommand = new AsyncRelayCommand<GameModel>(RestoreBackupAsync, CanRestoreBackup);
        ScanInstalledGamesCommand = new AsyncRelayCommand(ScanInstalledGamesAsync, CanScanInstalledGames);
        AddToBlacklistCommand = new AsyncRelayCommand<GameModel>(AddToBlacklistAsync, CanAddToBlacklist);
        AddToWhitelistCommand = new AsyncRelayCommand<GameModel>(AddToWhitelistAsync, CanAddToWhitelist);
        CreateBackupCommand = new AsyncRelayCommand<GameModel>(CreateBackupAsync, CanCreateBackup);
        CreateProtectedBackupCommand = new AsyncRelayCommand<GameModel>(CreateProtectedBackupAsync, CanCreateBackup);

        ToggleGameEnabledCommand = new Services.RelayCommand<GameModel>(ToggleGameEnabled, CanToggleGameEnabled);
        HideGameCommand = new Services.RelayCommand<GameModel>(HideGame, CanHideGame);
        HideSelectedCommand = new RelayCommand(HideSelected, CanHideSelected);

        EnableSelectedCommand = new RelayCommand(EnableSelected, CanEnableSelected);
        DisableSelectedCommand = new RelayCommand(DisableSelected, CanDisableSelected);
        AddSelectedToWhitelistCommand = new AsyncRelayCommand(AddSelectedToWhitelistAsync, CanAddSelectedToWhitelist);
        AddSelectedToBlacklistCommand = new AsyncRelayCommand(AddSelectedToBlacklistAsync, CanAddSelectedToBlacklist);

        ToggleShowHiddenGamesCommand = new RelayCommand(ToggleShowHiddenGames);
        ToggleShowOnlyEnabledCommand = new RelayCommand(ToggleShowOnlyEnabled);
        ToggleShowOnlyDisabledCommand = new RelayCommand(ToggleShowOnlyDisabled);

        _installedGamesScanner = new InstalledGamesScanner(_blacklistService, _whitelistService);

        _ = LoadGamesAsync();
    }

    public ObservableCollection<GameModel>? Games
    {
        get => _games;
        private set
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
                RaiseAllCanExecuteChanged();
            }
        }
    }

    public bool IsNotLoading => !IsLoading;

    private IEnumerable<GameModel> SelectedGames => Games?.Where(g => g.IsSelected) ?? [];

    public bool ShowHiddenGames
    {
        get => _showHiddenGames;
        set
        {
            if (_showHiddenGames != value)
            {
                _showHiddenGames = value;
                OnPropertyChanged();
                if (Games != null)
                    foreach (var g in Games)
                        g.ShowHiddenGames = value;
                if (FilteredGames != null)
                    foreach (var g in FilteredGames)
                        g.ShowHiddenGames = value;
                UpdateFilteredGames();
            }
        }
    }

    public bool ShowOnlyEnabled
    {
        get => _showOnlyEnabled;
        set
        {
            if (_showOnlyEnabled != value)
            {
                _showOnlyEnabled = value;
                OnPropertyChanged();
                UpdateFilteredGames();
            }
        }
    }

    public bool ShowOnlyDisabled
    {
        get => _showOnlyDisabled;
        set
        {
            if (_showOnlyDisabled != value)
            {
                _showOnlyDisabled = value;
                OnPropertyChanged();
                UpdateFilteredGames();
            }
        }
    }

    public IAsyncRelayCommand DeleteGameCommand { get; }
    public ICommand EditGameCommand { get; }
    public ICommand OpenSaveCommand { get; }
    public IAsyncRelayCommand RestoreBackupCommand { get; }
    public IAsyncRelayCommand ScanInstalledGamesCommand { get; }
    public IAsyncRelayCommand<GameModel> AddToBlacklistCommand { get; }
    public IAsyncRelayCommand<GameModel> AddToWhitelistCommand { get; }
    public ICommand ToggleGameEnabledCommand { get; }
    public ICommand EnableSelectedCommand { get; }
    public ICommand DisableSelectedCommand { get; }
    public ICommand AddSelectedToWhitelistCommand { get; }
    public ICommand AddSelectedToBlacklistCommand { get; }
    public ICommand HideGameCommand { get; }
    public ICommand HideSelectedCommand { get; }
    public ICommand ToggleShowHiddenGamesCommand { get; }
    public ICommand ToggleShowOnlyEnabledCommand { get; }
    public ICommand ToggleShowOnlyDisabledCommand { get; }
    public IAsyncRelayCommand<GameModel> CreateBackupCommand { get; }
    public IAsyncRelayCommand<GameModel> CreateProtectedBackupCommand { get; }

    public event EventHandler<GameModel>? EditGameRequested;

    private void Games_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        try
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    await SaveGamesAsync().ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    LogError(ex, "Error saving games in collection changed");
                }
            });

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
            var gamesList = await _gamesService.LoadGamesAsync().ConfigureAwait(false);
            var sortedGamesList = gamesList.OrderBy(g => g?.GameName, StringComparer.OrdinalIgnoreCase).ToList();
            Games = new ObservableCollection<GameModel>(sortedGamesList!);
            foreach (var game in Games)
            {
                game.PropertyChanged += Game_PropertyChanged;
                UpdateBackupCount(game);
            }

            await RemoveBlacklistedGamesAsync().ConfigureAwait(false);
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

    private void Game_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(GameModel.IsEnabled))
            try
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await SaveGamesAsync().ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        LogError(ex, "Error saving games after property change");
                    }
                });
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
            await Dispatcher.UIThread.InvokeAsync(() =>
                _notificationService.ShowTaskRunning(Resources.AddGameOverlay_Save));

            await _gamesService.SaveGamesAsync(Games);
        }
        catch (Exception ex)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
                _notificationService.ShowTaskError(Resources.AddGameOverlay_Save));
            LogError(ex, "Error saving games");
            throw;
        }

        await Dispatcher.UIThread.InvokeAsync(() =>
            _notificationService.ShowTaskCompleted(Resources.AddGameOverlay_Save));
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

            var result = await dialog.ShowAsync().ConfigureAwait(false);

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
            var dialog = new ContentDialog
            {
                Title = Resources.GamesView_ConfirmRestoreTitle,
                Content = Resources.GamesView_ConfirmRestoreDec,
                PrimaryButtonText = Resources.GamesView_ConfirmRestoreConfirm,
                CloseButtonText = Resources.GamesView_ConfirmRestoreCancel
            };
            var result = await dialog.ShowAsync().ConfigureAwait(false);
            if (result != ContentDialogResult.Primary)
                return;

            await Dispatcher.UIThread.InvokeAsync(() =>
                _notificationService.ShowTaskRunning(Resources.GamesView_RestoreBackup));

            await _backupService.RestoreLatestBackupAsync(game).ConfigureAwait(false);
            LogInformation("Successfully restored backup for game {GameName}", game.GameName);

            await Dispatcher.UIThread.InvokeAsync(() =>
                _notificationService.ShowTaskCompleted(Resources.GamesView_RestoreBackup));
        }
        catch (Exception ex)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
                _notificationService.ShowTaskError(Resources.GamesView_RestoreBackup));
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

        IEnumerable<GameModel> filtered = Games;

        filtered = filtered.Where(g => ShowHiddenGames ? g.IsHidden : !g.IsHidden);

        if (ShowOnlyEnabled)
            filtered = filtered.Where(g => g.IsEnabled);
        if (ShowOnlyDisabled)
            filtered = filtered.Where(g => !g.IsEnabled);

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var lower = SearchText.ToLowerInvariant();
            filtered = filtered.Where(g =>
                (!string.IsNullOrEmpty(g.GameName) && g.GameName.ToLowerInvariant().Contains(lower)) ||
                (!string.IsNullOrEmpty(g.SavePath) && g.SavePath.ToLowerInvariant().Contains(lower)) ||
                (!string.IsNullOrEmpty(g.GameExe) && g.GameExe.ToLowerInvariant().Contains(lower))
            );
        }

        FilteredGames = new ObservableCollection<GameModel>(filtered);
        if (FilteredGames != null)
            foreach (var g in FilteredGames)
                g.ShowHiddenGames = ShowHiddenGames;
    }

    [SupportedOSPlatform("windows")]
    private async Task ScanInstalledGamesAsync()
    {
        await Dispatcher.UIThread.InvokeAsync(() =>
            _notificationService.ShowTaskRunning(Resources.GamesView_ScanInstalledGames));

        try
        {
            IsLoading = true;
            var foundGames = await _installedGamesScanner.ScanForInstalledGamesAsync().ConfigureAwait(false);
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

                await Dispatcher.UIThread.InvokeAsync(() =>
                    _notificationService.ShowTaskCompleted(Resources.GamesView_ScanInstalledGames));
            }

            await RemoveBlacklistedGamesAsync();
        }
        catch (Exception ex)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
                _notificationService.ShowTaskError(Resources.GamesView_ScanInstalledGames));
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

        await Dispatcher.UIThread.InvokeAsync(() =>
            _notificationService.ShowTaskRunning(Resources.GamesView_AddToBlacklist));

        try
        {
            if (game.GameName != null)
            {
                await _blacklistService.AddToBlacklistAsync(game.GameName);
                await _blacklistService.ContributeToServerAsync(game.GameName);

                Games?.Remove(game);

                LogInformation("Added {GameName} to blacklist and contributed to server", game.GameName);

                await Dispatcher.UIThread.InvokeAsync(() =>
                    _notificationService.ShowTaskCompleted(Resources.GamesView_AddToBlacklist));
            }
        }
        catch (Exception ex)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
                _notificationService.ShowTaskError(Resources.GamesView_AddToBlacklist));
            LogError(ex, "Error adding {GameName} to blacklist", game.GameName);
        }
    }

    private async Task AddToWhitelistAsync(GameModel? game)
    {
        if (game == null) return;

        await Dispatcher.UIThread.InvokeAsync(() =>
            _notificationService.ShowTaskRunning(Resources.GamesView_AddToWhitelist));

        try
        {
            var whitelistEntry = WhitelistEntry.FromGameModel(game);
            await _whitelistService.AddToWhitelistAsync(whitelistEntry);
            await _whitelistService.ContributeToServerAsync(whitelistEntry);

            LogInformation("Added {GameName} to whitelist and contributed to server", game.GameName);

            await Dispatcher.UIThread.InvokeAsync(() =>
                _notificationService.ShowTaskCompleted(Resources.GamesView_AddToWhitelist));
        }
        catch (Exception ex)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
                _notificationService.ShowTaskError(Resources.GamesView_AddToWhitelist));
            LogError(ex, "Error adding {GameName} to whitelist", game.GameName);
        }
    }

    private async Task CreateBackupAsync(GameModel? game)
    {
        if (game == null) return;

        await Dispatcher.UIThread.InvokeAsync(() =>
            _notificationService.ShowTaskRunning(Resources.CreateBackup));

        try
        {
            await _backupManager.ProcessGameBackupAsync(game, false).ConfigureAwait(false);
            LogInformation("Backup created for game {GameName}", game.GameName);

            await Dispatcher.UIThread.InvokeAsync(() =>
                _notificationService.ShowTaskCompleted(Resources.CreateBackup));

            UpdateBackupCount(game);
        }
        catch (Exception ex)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
                _notificationService.ShowTaskError(Resources.CreateBackup));
            LogError(ex, "Error creating backup for game {GameName}", game.GameName);
        }
    }

    private async Task CreateProtectedBackupAsync(GameModel? game)
    {
        if (game == null) return;

        await Dispatcher.UIThread.InvokeAsync(() =>
            _notificationService.ShowTaskRunning(Resources.CreateProtectedBackup));

        try
        {
            await _backupManager.ProcessGameBackupAsync(game, true).ConfigureAwait(false);
            LogInformation("Protected backup created for game {GameName}", game.GameName);

            await Dispatcher.UIThread.InvokeAsync(() =>
                _notificationService.ShowTaskCompleted(Resources.CreateProtectedBackup));

            UpdateBackupCount(game);
        }
        catch (Exception ex)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
                _notificationService.ShowTaskError(Resources.CreateProtectedBackup));
            LogError(ex, "Error creating protected backup for game {GameName}", game.GameName);
        }
    }

    private bool CanCreateBackup(GameModel? game)
    {
        return IsNotLoading && game is { IsEnabled: true };
    }

    private void ToggleGameEnabled(GameModel? game)
    {
        if (game == null) return;
        game.IsEnabled = !game.IsEnabled;
    }

    private bool CanToggleGameEnabled(GameModel? game)
    {
        return game != null && !IsLoading;
    }

    private void EnableSelected()
    {
        foreach (var game in SelectedGames)
            game.IsEnabled = true;
    }

    private void DisableSelected()
    {
        foreach (var game in SelectedGames)
            game.IsEnabled = false;
    }

    private bool CanEnableSelected()
    {
        return SelectedGames.Any();
    }

    private bool CanDisableSelected()
    {
        return SelectedGames.Any();
    }

    private async Task AddSelectedToWhitelistAsync()
    {
        foreach (var game in SelectedGames)
            await AddToWhitelistAsync(game);
    }

    private async Task AddSelectedToBlacklistAsync()
    {
        foreach (var game in SelectedGames)
            await AddToBlacklistAsync(game);
    }

    private bool CanAddSelectedToWhitelist()
    {
        return SelectedGames.Any();
    }

    private bool CanAddSelectedToBlacklist()
    {
        return SelectedGames.Any();
    }

    private void HideGame(GameModel? game)
    {
        if (game == null) return;
        game.IsHidden = !ShowHiddenGames;
        UpdateFilteredGames();
        _ = Task.Run(async () =>
        {
            try
            {
                await SaveGamesAsync();
            }
            catch (Exception ex)
            {
                LogError(ex, "Error saving games after hiding game");
            }
        });
    }

    private bool CanHideGame(GameModel? game)
    {
        if (game == null || IsLoading)
            return false;
        if (ShowHiddenGames)
            return game.IsHidden;
        return !game.IsHidden;
    }

    private void HideSelected()
    {
        foreach (var game in SelectedGames)
            if (ShowHiddenGames)
            {
                if (game.IsHidden)
                    game.IsHidden = false;
            }
            else
            {
                if (!game.IsHidden)
                    game.IsHidden = true;
            }

        UpdateFilteredGames();
        _ = Task.Run(async () =>
        {
            try
            {
                await SaveGamesAsync();
            }
            catch (Exception ex)
            {
                LogError(ex, "Error saving games after hiding selected");
            }
        });
    }

    private bool CanHideSelected()
    {
        if (ShowHiddenGames)
            return SelectedGames.Any(g => g.IsHidden);
        return SelectedGames.Any(g => !g.IsHidden);
    }

    private void ToggleShowHiddenGames()
    {
        ShowHiddenGames = !ShowHiddenGames;
    }

    private void ToggleShowOnlyEnabled()
    {
        ShowOnlyEnabled = !ShowOnlyEnabled;
        if (ShowOnlyEnabled && ShowOnlyDisabled)
            ShowOnlyDisabled = false;
    }

    private void ToggleShowOnlyDisabled()
    {
        ShowOnlyDisabled = !ShowOnlyDisabled;
        if (ShowOnlyEnabled && ShowOnlyDisabled)
            ShowOnlyEnabled = false;
    }

    private void RaiseAllCanExecuteChanged()
    {
        DeleteGameCommand.NotifyCanExecuteChanged();
        (EditGameCommand as Services.RelayCommand<GameModel>)?.RaiseCanExecuteChanged();
        (OpenSaveCommand as Services.RelayCommand<GameModel>)?.RaiseCanExecuteChanged();
        RestoreBackupCommand.NotifyCanExecuteChanged();
        ScanInstalledGamesCommand.NotifyCanExecuteChanged();
        AddToBlacklistCommand.NotifyCanExecuteChanged();
        AddToWhitelistCommand.NotifyCanExecuteChanged();
        CreateBackupCommand.NotifyCanExecuteChanged();
        CreateProtectedBackupCommand.NotifyCanExecuteChanged();
        (ToggleShowHiddenGamesCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (ToggleShowOnlyEnabledCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (ToggleShowOnlyDisabledCommand as RelayCommand)?.RaiseCanExecuteChanged();
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
        return game != null && !IsLoading && _backupService.GetBackupCount(game) > 0;
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