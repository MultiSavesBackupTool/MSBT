using System;
using System.Net.Http;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FluentAvalonia.UI.Controls;
using Microsoft.Extensions.Logging;
using Multi_Saves_Backup_Tool.Services;
using Multi_Saves_Backup_Tool.Services.GameDiscovery;

namespace Multi_Saves_Backup_Tool.ViewModels;

public partial class MainWindowViewModel : ViewModelBase, IDisposable
{
    private readonly ILogger<MainWindowViewModel> _logger;
    private readonly UpdateService _updateService;

    [ObservableProperty] private ViewModelBase? _currentViewModel;
    private bool _disposed;

    [ObservableProperty] private bool _isUpdateAvailable;

    [ObservableProperty] private NavigationViewItem? _selectedMenuItem;
    private string? _updateDownloadUrl = string.Empty;

    [ObservableProperty] private string _updateMessage = string.Empty;

    [SupportedOSPlatform("windows")]
    public MainWindowViewModel(Window mainWindow, IGamesService gamesService,
        IBackupService backupService, BackupManager backupManager, ISettingsService settingsService,
        IBlacklistService blacklistService, IWhitelistService whitelistService, ILogger<StatsViewModel> statsLogger,
        ILogger<MainWindowViewModel> logger,
        ILogger<MonitoringViewModel> monitoringLogger, ILogger<GamesViewModel> gamesLogger,
        ILogger<SettingsViewModel> settingsLogger)
    {
        _updateService = new UpdateService();
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        try
        {
            MonitoringViewModel = new MonitoringViewModel(backupManager, monitoringLogger);
            GamesViewModel = new GamesViewModel(gamesService, backupService, blacklistService, whitelistService,
                gamesLogger);
            StatsViewModel = new StatsViewModel(gamesService, backupService, settingsService, statsLogger);
            SettingsViewModel = new SettingsViewModel(mainWindow.StorageProvider, blacklistService, whitelistService,
                settingsLogger);
            CurrentViewModel = MonitoringViewModel;

            _ = CheckForUpdatesOnStartupAsync();
            MessengerService.Instance.Subscribe<NavigateToSettingsMessage>(OnNavigateToSettingsRequested);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initializing MainWindowViewModel");
            throw;
        }
    }

    private MonitoringViewModel MonitoringViewModel { get; }
    private GamesViewModel GamesViewModel { get; }
    private StatsViewModel StatsViewModel { get; }
    private SettingsViewModel SettingsViewModel { get; }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    private async Task CheckForUpdatesOnStartupAsync()
    {
        try
        {
            _logger.LogDebug("Checking for updates on startup...");
            var (hasUpdate, latestVersion, downloadUrl) = await _updateService.CheckForUpdatesAsync();

            if (hasUpdate && !string.IsNullOrEmpty(downloadUrl))
            {
                IsUpdateAvailable = true;
                _logger.LogInformation("Update available: {LatestVersion}, URL: {DownloadUrl}", latestVersion,
                    downloadUrl);

                _updateDownloadUrl = downloadUrl;

                await DownloadAndInstallUpdateAsync();
            }
            else if (hasUpdate)
            {
                IsUpdateAvailable = false;
                _logger.LogWarning("Update available: {LatestVersion}, but no download URL.", latestVersion);
            }
            else if (downloadUrl == null &&
                     latestVersion == (_updateService.GetType().Assembly.GetName().Version?.ToString() ?? "1.0.0"))
            {
                IsUpdateAvailable = false;
                _logger.LogError("Error occurred while checking for updates.");
            }
            else
            {
                IsUpdateAvailable = false;
                _logger.LogDebug("No updates available.");
            }
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Network error while checking for updates - this is normal if offline");
            IsUpdateAvailable = false;
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogWarning(ex, "Update check timed out");
            IsUpdateAvailable = false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking for updates");
            IsUpdateAvailable = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanDownloadAndInstallUpdate))]
    private async Task DownloadAndInstallUpdateAsync()
    {
        if (!IsUpdateAvailable || string.IsNullOrEmpty(_updateDownloadUrl)) return;

        _logger.LogInformation("DownloadAndInstallUpdateCommand executed.");
        try
        {
            var success = await _updateService.DownloadAndInstallUpdateAsync(_updateDownloadUrl);
            if (!success)
            {
                IsUpdateAvailable = true;
                _logger.LogWarning("Update installation failed. Please try running the application as administrator.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during update installation");
            IsUpdateAvailable = true;
        }
    }

    private bool CanDownloadAndInstallUpdate()
    {
        return IsUpdateAvailable && !string.IsNullOrEmpty(_updateDownloadUrl);
    }

    partial void OnSelectedMenuItemChanged(NavigationViewItem? value)
    {
        if (value == null) return;

        try
        {
            CurrentViewModel = (value.Tag as string) switch
            {
                "monitoring" => MonitoringViewModel,
                "games" => GamesViewModel,
                "stats" => StatsViewModel,
                "settings" => SettingsViewModel,
                _ => CurrentViewModel
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error changing selected menu item");
        }
    }

    private void OnNavigateToSettingsRequested(NavigateToSettingsMessage message)
    {
        try
        {
            CurrentViewModel = SettingsViewModel;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error navigating to settings");
        }
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
                try
                {
                    _updateService.Dispose();
                    MessengerService.Instance.Unsubscribe<NavigateToSettingsMessage>(OnNavigateToSettingsRequested);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during disposal");
                }

            _disposed = true;
        }
    }
}