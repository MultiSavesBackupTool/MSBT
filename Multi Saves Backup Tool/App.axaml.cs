using System;
using System.Runtime.Versioning;
using System.Threading.Tasks;
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
    private bool _isShuttingDown;
    private IServiceProvider? _serviceProvider;

    public App()
    {
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            try
            {
                var logger = _serviceProvider?.GetService<ILogger<App>>();
                logger?.LogCritical(args.ExceptionObject as Exception, "Unhandled exception in AppDomain");
            }
            catch
            {
                // Ignore logging errors during shutdown
            }
        };
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            try
            {
                var logger = _serviceProvider?.GetService<ILogger<App>>();
                logger?.LogCritical(args.Exception, "Unobserved task exception");
            }
            catch
            {
                // Ignore logging errors during shutdown
            }
        };
    }

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
        services.AddSingleton<MonitoringViewModel>();
        services.AddSingleton<TrayService>(provider =>
            new TrayService(
                provider.GetRequiredService<MonitoringViewModel>(),
                provider.GetRequiredService<IGamesService>(),
                provider.GetRequiredService<BackupManager>(),
                provider.GetRequiredService<ILogger<TrayService>>()
            )
        );
    }

    [SupportedOSPlatform("windows")]
    public override async void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            if (_serviceProvider == null) throw new InvalidOperationException("Service provider is not initialized");

            try
            {
                var trayService = _serviceProvider.GetRequiredService<TrayService>();
                var gamesService = _serviceProvider.GetRequiredService<IGamesService>();
                var backupService = _serviceProvider.GetRequiredService<IBackupService>();
                var settingsService = _serviceProvider.GetRequiredService<ISettingsService>();
                var statsLogger = _serviceProvider.GetRequiredService<ILogger<StatsViewModel>>();
                var mainWindowLogger = _serviceProvider.GetRequiredService<ILogger<MainWindowViewModel>>();
                var monitoringLogger = _serviceProvider.GetRequiredService<ILogger<MonitoringViewModel>>();
                var gamesLogger = _serviceProvider.GetRequiredService<ILogger<GamesViewModel>>();
                var settingsLogger = _serviceProvider.GetRequiredService<ILogger<SettingsViewModel>>();
                _backupManager = _serviceProvider.GetRequiredService<BackupManager>();

                var mainWindow = new MainWindow();
                var mainViewModel = new MainWindowViewModel(mainWindow, gamesService, backupService,
                    _backupManager, settingsService, statsLogger, mainWindowLogger, monitoringLogger, gamesLogger,
                    settingsLogger);

                mainWindow.DataContext = mainViewModel;
                desktop.MainWindow = mainWindow;
                trayService.Initialize();

                try
                {
                    await _backupManager.StartAsync();
                }
                catch (Exception ex)
                {
                    var logger = _serviceProvider.GetRequiredService<ILogger<App>>();
                    logger.LogError(ex, "Failed to start backup manager");
                }

                desktop.ShutdownRequested += (_, e) =>
                {
                    if (_isShuttingDown) return;
                    _isShuttingDown = true;
                    var logger = _serviceProvider.GetRequiredService<ILogger<App>>();
                    logger.LogInformation("Shutdown requested. Starting cleanup...");
                    e.Cancel = true;

                    try
                    {
                        if (_backupManager != null)
                            try
                            {
                                _backupManager.StopAsync().GetAwaiter().GetResult();
                                _backupManager.Dispose();
                            }
                            catch (Exception ex)
                            {
                                logger.LogError(ex, "Error during backup manager shutdown");
                            }

                        if (trayService is IDisposable disposableTray)
                            try
                            {
                                disposableTray.Dispose();
                            }
                            catch (Exception ex)
                            {
                                logger.LogError(ex, "Error disposing tray");
                            }

                        var disposableServices = _serviceProvider.GetServices<IDisposable>();
                        foreach (var service in disposableServices)
                            try
                            {
                                service.Dispose();
                            }
                            catch (Exception ex)
                            {
                                logger.LogError(ex, "Error disposing service: {ServiceType}", service.GetType().Name);
                            }

                        if (_serviceProvider is IDisposable disposableProvider)
                            try
                            {
                                disposableProvider.Dispose();
                            }
                            catch (Exception ex)
                            {
                                logger.LogError(ex, "Error disposing service provider");
                            }
                    }
                    catch (Exception ex)
                    {
                        logger.LogCritical(ex, "Critical error during shutdown");
                    }

                    logger.LogInformation("Cleanup complete. Exiting application.");
                    Environment.Exit(0);
                };
            }
            catch (Exception ex)
            {
                var logger = _serviceProvider?.GetService<ILogger<App>>();
                logger?.LogCritical(ex, "Critical error during MainWindow initialization");
                throw;
            }
        }

        base.OnFrameworkInitializationCompleted();
    }
}