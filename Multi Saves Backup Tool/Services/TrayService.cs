using System;
using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform;

public class NavigateToSettingsMessage
{
}

namespace Multi_Saves_Backup_Tool.Services
{
    public interface ITrayService
    {
        bool IsVisible { get; set; }
        void Initialize();
        void ShowBalloonTip(string title, string message);
        void UpdateTooltip(string tooltip);
    }

    public class TrayService : ITrayService
    {
        private Window? _mainWindow;
        private TrayIcon? _trayIcon;

        public bool IsVisible { get; set; } = true;

        public void Initialize()
        {
            if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
                return;

            _mainWindow = desktop.MainWindow;
            if (_mainWindow == null) return;

            var trayIcon = new TrayIcon
            {
                Icon = new WindowIcon(
                    AssetLoader.Open(new Uri("avares://Multi Saves Backup Tool/Assets/avalonia-logo.ico"))),
                ToolTipText = "Multi Saves Backup Tool",
                IsVisible = true
            };

            trayIcon.Menu = new NativeMenu
            {
                Items =
                {
                    new NativeMenuItem("Показать/Скрыть")
                    {
                        Command = new RelayCommand(ToggleWindowVisibility)
                    },
                    new NativeMenuItemSeparator(),
                    new NativeMenuItem("Настройки")
                    {
                        Command = new RelayCommand(OpenSettings)
                    },
                    new NativeMenuItemSeparator(),
                    new NativeMenuItem("Выход")
                    {
                        Command = new RelayCommand(ExitApplication)
                    }
                }
            };

            trayIcon.Clicked += (_, _) => ToggleWindowVisibility();

            _trayIcon = trayIcon;

            _mainWindow.Closing += OnMainWindowClosing;
        }

        public void ShowBalloonTip(string title, string message)
        {
            if (OperatingSystem.IsWindows())
                ShowWindowsNotification(title, message);
            else if (OperatingSystem.IsMacOS())
                ShowMacOsNotification(title, message);
            else if (OperatingSystem.IsLinux()) ShowLinuxNotification(title, message);
        }

        public void UpdateTooltip(string tooltip)
        {
            if (_trayIcon != null) _trayIcon.ToolTipText = tooltip;
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

        private void ExitApplication()
        {
            IsVisible = false;
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                desktop.Shutdown();
        }

        private void ShowWindowsNotification(string title, string message)
        {
            try
            {
                var script = $@"
                    Add-Type -AssemblyName System.Windows.Forms
                    $notify = New-Object System.Windows.Forms.NotifyIcon
                    $notify.Icon = [System.Drawing.SystemIcons]::Information
                    $notify.Visible = $true
                    $notify.ShowBalloonTip(3000, '{title}', '{message}', [System.Windows.Forms.ToolTipIcon]::Info)
                ";

                Process.Start(new ProcessStartInfo
                {
                    FileName = "powershell",
                    Arguments = $"-Command \"{script}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to show Windows notification: {ex.Message}");
            }
        }

        private void ShowMacOsNotification(string title, string message)
        {
            try
            {
                var script = $@"display notification ""{message}"" with title ""{title}""";
                Process.Start(new ProcessStartInfo
                {
                    FileName = "osascript",
                    Arguments = $"-e '{script}'",
                    UseShellExecute = false,
                    CreateNoWindow = true
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to show macOS notification: {ex.Message}");
            }
        }

        private void ShowLinuxNotification(string title, string message)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "notify-send",
                    Arguments = $"\"{title}\" \"{message}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to show Linux notification: {ex.Message}");
            }
        }
    }
}