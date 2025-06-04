using MultiSavesBackup.Service;
using MultiSavesBackup.Service.Models;
using MultiSavesBackup.Service.Services;

IHost host = Host.CreateDefaultBuilder(args)
    .UseWindowsService(options =>
    {
        options.ServiceName = "Multi Saves Backup Service";
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
