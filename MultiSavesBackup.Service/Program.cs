using MultiSavesBackup.Service;
using MultiSavesBackup.Service.Models;
using MultiSavesBackup.Service.Services;
using System.IO;

var mainAppDir = AppDomain.CurrentDomain.BaseDirectory;
var settingsPath = Path.Combine(mainAppDir, "settings.json");

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
    .ConfigureServices((context, services) =>
    {
        services.Configure<ServiceSettings>(context.Configuration);
        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddSingleton<IGamesService, GamesService>();
        services.AddSingleton<IBackupService, BackupService>();
        services.AddHostedService<BackupWorker>();
    })
    .Build();

await host.RunAsync();
