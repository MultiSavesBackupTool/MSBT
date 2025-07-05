using System;
using System.ComponentModel;
using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform;
using Microsoft.Extensions.Logging;
using Multi_Saves_Backup_Tool.ViewModels;
using Properties;

public class NavigateToSettingsMessage
{
}

namespace Multi_Saves_Backup_Tool.Services
{
    public class TrayService : ITrayService, IDisposable
    {
        private readonly BackupManager _backupManager;
        private readonly IGamesService _gamesService;
        private readonly ILogger<TrayService>? _logger;
        private readonly MonitoringViewModel _monitoringViewModel;
        private NativeMenuItem? _backupMenuItem;
        private NativeMenuItem? _backupProtectedMenuItem;
        private NativeMenuItem? _currentGameMenuItem;

        private bool _disposed;
        private Window? _mainWindow;
        private TrayIcon? _trayIcon;

        public TrayService(MonitoringViewModel monitoringViewModel, IGamesService gamesService,
            BackupManager backupManager, ILogger<TrayService> logger)
        {
            _monitoringViewModel = monitoringViewModel ?? throw new ArgumentNullException(nameof(monitoringViewModel));
            _gamesService = gamesService ?? throw new ArgumentNullException(nameof(gamesService));
            _backupManager = backupManager ?? throw new ArgumentNullException(nameof(backupManager));
            _logger = logger;
            _monitoringViewModel.PropertyChanged += MonitoringViewModelOnPropertyChanged;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        public bool IsVisible { get; set; } = true;

        public void Initialize()
        {
            try
            {
                if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
                {
                    _logger?.LogWarning("Application lifetime is not IClassicDesktopStyleApplicationLifetime");
                    return;
                }

                _mainWindow = desktop.MainWindow;
                if (_mainWindow == null)
                {
                    _logger?.LogWarning("Main window is null");
                    return;
                }

                var trayIcon = new TrayIcon
                {
                    Icon = new WindowIcon(
                        AssetLoader.Open(new Uri("avares://Multi Saves Backup Tool/Assets/msbt.ico"))),
                    ToolTipText = "Multi Saves Backup Tool",
                    IsVisible = true
                };

                _currentGameMenuItem = new NativeMenuItem(Resources.CurrentGameNotRunning) { IsEnabled = false };
                _backupMenuItem = new NativeMenuItem(Resources.CreateBackup);
                _backupMenuItem.Click += (_, _) => CreateBackup(false);
                _backupProtectedMenuItem = new NativeMenuItem(Resources.CreateProtectedBackup);
                _backupProtectedMenuItem.Click += (_, _) => CreateBackup(true);

                trayIcon.Menu = new NativeMenu
                {
                    Items =
                    {
                        _currentGameMenuItem,
                        new NativeMenuItemSeparator(),
                        _backupMenuItem,
                        _backupProtectedMenuItem,
                        new NativeMenuItemSeparator(),
                        new NativeMenuItem(Resources.ShowHide)
                        {
                            Command = new RelayCommand(ToggleWindowVisibility)
                        },
                        new NativeMenuItemSeparator(),
                        new NativeMenuItem(Resources.Settings)
                        {
                            Command = new RelayCommand(OpenSettings)
                        },
                        new NativeMenuItemSeparator(),
                        new NativeMenuItem(Resources.Exit)
                        {
                            Command = new RelayCommand(ExitApplication)
                        }
                    }
                };

                trayIcon.Clicked += (_, _) => ToggleWindowVisibility();

                _trayIcon = trayIcon;

                _mainWindow.Closing += OnMainWindowClosing;
                UpdateCurrentGameMenu();

                _logger?.LogInformation("Tray service initialized successfully");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error initializing tray service");
            }
        }

        public void UpdateTooltip(string tooltip)
        {
            try
            {
                if (_trayIcon != null && !string.IsNullOrWhiteSpace(tooltip)) _trayIcon.ToolTipText = tooltip;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error updating tray tooltip");
            }
        }

        private void MonitoringViewModelOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            try
            {
                if (e.PropertyName == nameof(MonitoringViewModel.CurrentGameName)) UpdateCurrentGameMenu();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error handling monitoring view model property change");
            }
        }

        private void UpdateCurrentGameMenu()
        {
            try
            {
                if (_currentGameMenuItem == null) return;
                var gameName = _monitoringViewModel.CurrentGameName;

                var notRunningText = Resources.CurrentGameNotRunning;
                var runningFormatText = Resources.CurrentGameRunning;

                var headerText = string.IsNullOrEmpty(gameName)
                    ? notRunningText
                    : string.Format(runningFormatText, gameName);

                _currentGameMenuItem.Header = headerText;
                _backupMenuItem!.IsEnabled = !string.IsNullOrEmpty(gameName);
                _backupProtectedMenuItem!.IsEnabled = !string.IsNullOrEmpty(gameName);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error updating current game menu");
            }
        }

        private async void CreateBackup(bool isProtected)
        {
            try
            {
                var gameName = _monitoringViewModel.CurrentGameName;
                if (string.IsNullOrEmpty(gameName))
                {
                    _logger?.LogWarning("Cannot create backup: no current game");
                    return;
                }

                var game = await _gamesService.GetGameByNameAsync(gameName);

                if (game == null)
                {
                    _logger?.LogWarning("Game not found: {GameName}", gameName);
                    return;
                }

                await _backupManager.ProcessGameBackupAsync(game, isProtected);
                _logger?.LogInformation("Backup created for game {GameName}, protected: {IsProtected}", gameName,
                    isProtected);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error creating backup");
            }
        }

        private void OnMainWindowClosing(object? sender, WindowClosingEventArgs e)
        {
            if (_mainWindow != null && IsVisible)
            {
                e.Cancel = true;
                _mainWindow.Hide();
            }
        }

        private void ToggleWindowVisibility()
        {
            if (_mainWindow == null) return;

            if (_mainWindow.IsVisible)
            {
                _mainWindow.Hide();
            }
            else
            {
                _mainWindow.Show();
                _mainWindow.Activate();
                _mainWindow.BringIntoView();

                if (OperatingSystem.IsWindows())
                {
                    _mainWindow.Topmost = true;
                    _mainWindow.Topmost = false;
                }
            }
        }

        private void OpenSettings()
        {
            ToggleWindowVisibility();
            MessengerService.Instance.Send(new NavigateToSettingsMessage());
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    if (_trayIcon != null)
                    {
                        _trayIcon.Dispose();
                        _trayIcon = null;
                    }

                    _monitoringViewModel.PropertyChanged -= MonitoringViewModelOnPropertyChanged;

                    if (_mainWindow != null)
                    {
                        _mainWindow.Closing -= OnMainWindowClosing;
                        _mainWindow = null;
                    }
                }

                _disposed = true;
            }
        }

        private void ExitApplication()
        {
            try
            {
                IsVisible = false;
                if (_trayIcon != null) _trayIcon.IsVisible = false;
                Dispose();
                if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                    desktop.Shutdown();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error during application exit: {ex}");
            }
        }
    }
}