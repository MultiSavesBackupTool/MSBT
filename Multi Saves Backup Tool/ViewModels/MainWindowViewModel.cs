using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FluentAvalonia.UI.Controls;
using Microsoft.Extensions.Logging;
using Multi_Saves_Backup_Tool.Services;

namespace Multi_Saves_Backup_Tool.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly ITrayService _trayService;
    private readonly UpdateService _updateService;

    [ObservableProperty] private ViewModelBase? _currentViewModel;

    [ObservableProperty] private bool _isUpdateAvailable;

    [ObservableProperty] private NavigationViewItem? _selectedMenuItem;
    private string? _updateDownloadUrl;

    [ObservableProperty] private string _updateMessage = string.Empty;

    public MainWindowViewModel(Window mainWindow, ITrayService trayService, IGamesService gamesService,
        IBackupService backupService, BackupManager backupManager, ISettingsService settingsService,
        ILogger<MainWindowViewModel> logger, ILogger<StatsViewModel> statsLogger)
    {
        _trayService = trayService;
        _updateService = new UpdateService();

        MonitoringViewModel = new MonitoringViewModel(backupManager);
        GamesViewModel = new GamesViewModel(gamesService, backupService);
        StatsViewModel = new StatsViewModel(gamesService, backupService, settingsService, statsLogger);
        SettingsViewModel = new SettingsViewModel(mainWindow.StorageProvider);
        CurrentViewModel = MonitoringViewModel;

        _ = CheckForUpdatesOnStartupAsync();
        MessengerService.Instance.Subscribe<NavigateToSettingsMessage>(OnNavigateToSettingsRequested);
    }

    public MonitoringViewModel MonitoringViewModel { get; }
    public GamesViewModel GamesViewModel { get; }
    public StatsViewModel StatsViewModel { get; }
    public SettingsViewModel SettingsViewModel { get; }

    private async Task CheckForUpdatesOnStartupAsync()
    {
        Debug.WriteLine("Checking for updates on startup...");
        var (hasUpdate, latestVersion, downloadUrl) = await _updateService.CheckForUpdatesAsync();

        if (hasUpdate && !string.IsNullOrEmpty(downloadUrl))
        {
            IsUpdateAvailable = true;
            _updateDownloadUrl = downloadUrl;
            Debug.WriteLine($"Update available: {latestVersion}, URL: {downloadUrl}");

            await DownloadAndInstallUpdateAsync();
        }
        else if (hasUpdate)
        {
            IsUpdateAvailable = false;
            _updateDownloadUrl = null;
            Debug.WriteLine($"Update available: {latestVersion}, but no download URL.");
        }
        else if (downloadUrl == null &&
                 latestVersion == (_updateServiceGetType().Assembly.GetName().Version?.ToString() ?? "1.0.0"))
        {
            IsUpdateAvailable = false;
            _updateDownloadUrl = null;
            Debug.WriteLine("Error occurred while checking for updates.");
        }
        else
        {
            IsUpdateAvailable = false;
            _updateDownloadUrl = null;
            Debug.WriteLine("No updates available.");
        }
    }

    [RelayCommand(CanExecute = nameof(CanDownloadAndInstallUpdate))]
    private async Task DownloadAndInstallUpdateAsync()
    {
        if (!IsUpdateAvailable || string.IsNullOrEmpty(_updateDownloadUrl)) return;

        Debug.WriteLine("DownloadAndInstallUpdateCommand executed.");
        try
        {
            var success = await _updateService.DownloadAndInstallUpdateAsync(_updateDownloadUrl);
            if (!success)
            {
                IsUpdateAvailable = true;
                Debug.WriteLine("Update installation failed. Please try running the application as administrator.");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error during update installation: {ex.Message}");
            IsUpdateAvailable = true;
        }
    }

    private bool CanDownloadAndInstallUpdate()
    {
        return IsUpdateAvailable && !string.IsNullOrEmpty(_updateDownloadUrl);
    }

    partial void OnSelectedMenuItemChanged(NavigationViewItem? oldValue, NavigationViewItem? newValue)
    {
        if (newValue == null) return;

        CurrentViewModel = (newValue.Tag as string) switch
        {
            "monitoring" => MonitoringViewModel,
            "games" => GamesViewModel,
            "stats" => StatsViewModel,
            "settings" => SettingsViewModel,
            _ => CurrentViewModel
        };
    }

    private void OnNavigateToSettingsRequested(NavigateToSettingsMessage message)
    {
        CurrentViewModel = SettingsViewModel;
    }

    private Type _updateServiceGetType()
    {
        return _updateService.GetType();
    }
}