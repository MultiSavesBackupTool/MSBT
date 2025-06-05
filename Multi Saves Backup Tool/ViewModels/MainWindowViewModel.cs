using System;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FluentAvalonia.UI.Controls;
using Multi_Saves_Backup_Tool.Services;
using System.Threading.Tasks;
using Properties;
using System.Diagnostics;

namespace Multi_Saves_Backup_Tool.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly UpdateService _updateService;
    private string? _updateDownloadUrl;

    [ObservableProperty] private ViewModelBase? _currentViewModel;

    [ObservableProperty] private NavigationViewItem? _selectedMenuItem;

    [ObservableProperty] private bool _isUpdateAvailable;

    [ObservableProperty] private string _updateMessage = string.Empty;

    public MainWindowViewModel()
    {
        var window = new Window();
        _updateService = new UpdateService();
        
        MonitoringViewModel = new MonitoringViewModel();
        GamesViewModel = new GamesViewModel();
        SettingsViewModel = new SettingsViewModel(window.StorageProvider);
        CurrentViewModel = MonitoringViewModel;

        _ = CheckForUpdatesOnStartupAsync();
    }

    public MainWindowViewModel(Window mainWindow)
    {
        _updateService = new UpdateService();

        MonitoringViewModel = new MonitoringViewModel();
        GamesViewModel = new GamesViewModel();
        SettingsViewModel = new SettingsViewModel(mainWindow.StorageProvider);
        CurrentViewModel = MonitoringViewModel;

        _ = CheckForUpdatesOnStartupAsync();
    }

    public MonitoringViewModel MonitoringViewModel { get; }
    public GamesViewModel GamesViewModel { get; }
    public SettingsViewModel SettingsViewModel { get; }

    private async Task CheckForUpdatesOnStartupAsync()
    {
        Debug.WriteLine("Checking for updates on startup...");
        UpdateMessage = Resources.MainWindow_CheckForUpdates;
        var (hasUpdate, latestVersion, downloadUrl) = await _updateService.CheckForUpdatesAsync();

        if (hasUpdate && !string.IsNullOrEmpty(downloadUrl))
        {
            IsUpdateAvailable = true;
            _updateDownloadUrl = downloadUrl;
            UpdateMessage = string.Format(Resources.MainWindow_UpdateAvailable, latestVersion);
            Debug.WriteLine($"Update available: {latestVersion}, URL: {downloadUrl}");
        }
        else if (hasUpdate)
        {
            IsUpdateAvailable = false;
            _updateDownloadUrl = null;
            UpdateMessage = string.Format(Resources.MainWindow_UpdateError, latestVersion);
            Debug.WriteLine($"Update available: {latestVersion}, but no download URL.");
        }
        else if (downloadUrl == null && latestVersion == (_updateServiceGetType().Assembly.GetName().Version?.ToString() ?? "1.0.0"))
        {
            UpdateMessage = Resources.MainWindow_UpdateError_General;
            IsUpdateAvailable = false;
            _updateDownloadUrl = null;
            Debug.WriteLine("Error occurred while checking for updates.");
        }
        else
        {
            IsUpdateAvailable = false;
            _updateDownloadUrl = null;
            UpdateMessage = Resources.MainWindow_NoUpdates;
            Debug.WriteLine("No updates available.");
        }
    }

    [RelayCommand(CanExecute = nameof(CanDownloadAndInstallUpdate))]
    private async Task DownloadAndInstallUpdateAsync()
    {
        if (!IsUpdateAvailable || string.IsNullOrEmpty(_updateDownloadUrl)) return;

        Debug.WriteLine("DownloadAndInstallUpdateCommand executed.");
        UpdateMessage = Resources.MainWindow_DownloadingUpdate;
        bool success = await _updateService.DownloadAndInstallUpdateAsync(_updateDownloadUrl);
        if (!success)
        {
            UpdateMessage = Resources.MainWindow_UpdateError_Install;
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
            "settings" => SettingsViewModel,
            _ => CurrentViewModel
        };
    }

    private Type _updateServiceGetType() => _updateService.GetType();
}