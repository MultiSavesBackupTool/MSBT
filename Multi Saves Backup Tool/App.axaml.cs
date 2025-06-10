using System;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Multi_Saves_Backup_Tool.Services;
using Multi_Saves_Backup_Tool.ViewModels;
using Multi_Saves_Backup_Tool.Views;
using Serilog;

namespace Multi_Saves_Backup_Tool;

public class App : Application
{
    private BackupManager? _backupManager;
    private IServiceProvider? _serviceProvider;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);

        var services = new ServiceCollection();
        ConfigureServices(services);
        _serviceProvider = services.BuildServiceProvider();
    }

    private void ConfigureServices(IServiceCollection services)
    {
        services.AddLogging(builder =>
        {
            builder.ClearProviders();
            builder.AddSerilog(Log.Logger);
        });

        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddSingleton<IGamesService, GamesService>();
        services.AddSingleton<IBackupService, BackupService>();
        services.AddSingleton<BackupManager>();
        services.AddSingleton<TrayService>();
    }

    public override async void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            if (_serviceProvider == null) return;
            var trayService = _serviceProvider.GetRequiredService<TrayService>();
            var gamesService = _serviceProvider.GetRequiredService<IGamesService>();
            var backupService = _serviceProvider.GetRequiredService<IBackupService>();
            var settingsService = _serviceProvider.GetRequiredService<ISettingsService>();
            var mainWindowLogger = _serviceProvider.GetRequiredService<ILogger<MainWindowViewModel>>();
            var statsLogger = _serviceProvider.GetRequiredService<ILogger<StatsViewModel>>();
            _backupManager = _serviceProvider.GetRequiredService<BackupManager>();

            var mainWindow = new MainWindow();
            var mainViewModel = new MainWindowViewModel(mainWindow, trayService, gamesService, backupService, _backupManager, settingsService, mainWindowLogger, statsLogger);

            mainWindow.DataContext = mainViewModel;
            desktop.MainWindow = mainWindow;
            trayService.Initialize();

            await _backupManager.StartAsync();

            desktop.ShutdownRequested += async (s, e) =>
            {
                if (_backupManager != null) await _backupManager.StopAsync();
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}