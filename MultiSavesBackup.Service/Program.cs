using MultiSavesBackup.Service;
using MultiSavesBackup.Service.Models;
using MultiSavesBackup.Service.Services;
using System.IO;
using Serilog;

var mainAppDir = AppDomain.CurrentDomain.BaseDirectory;
var settingsPath = Path.Combine(mainAppDir, "settings.json");

var configuration = new ConfigurationBuilder()
    .SetBasePath(mainAppDir)
    .AddJsonFile(settingsPath, optional: false, reloadOnChange: true)
    .Build();

var settings = configuration.Get<ServiceSettings>();

Log.Logger = new LoggerConfiguration()
    .WriteTo.File(
        Path.Combine(mainAppDir, "backup_service.log"),
        rollingInterval: RollingInterval.Day,
        outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff} {Level:u3}] {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

IHost host = Host.CreateDefaultBuilder(args)
    .UseWindowsService(options =>
    {
        options.ServiceName = "Multi Saves Backup Service";
    })
    .ConfigureAppConfiguration((context, config) =>
    {
        config.SetBasePath(mainAppDir)
              .AddJsonFile(settingsPath, optional: false, reloadOnChange: true);
    })
    .UseSerilog()
    .ConfigureServices((context, services) =>
    {
        services.Configure<ServiceSettings>(context.Configuration);
        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddSingleton<IGamesService, GamesService>();
        services.AddSingleton<IBackupService, BackupService>();
        services.AddHostedService<BackupWorker>();
    })
    .Build();

try
{
    await host.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Service terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
