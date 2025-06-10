using System;
using System.IO;
using System.Threading;
using Avalonia;
using Multi_Saves_Backup_Tool.Paths;
using Serilog;

namespace Multi_Saves_Backup_Tool;

internal sealed class Program
{
    private static Mutex? _mutex;

    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        const string appName = "MultiSavesBackupTool";
        _mutex = new Mutex(true, appName, out bool createdNew);

        if (!createdNew)
        {
            return;
        }

        try
        {
            var dataAppDir = AppPaths.DataPath;

            Log.Logger = new LoggerConfiguration()
                .WriteTo.File(
                    Path.Combine(dataAppDir, "backup_service.log"),
                    rollingInterval: RollingInterval.Day,
                    outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff} {Level:u3}] {Message:lj}{NewLine}{Exception}")
                .CreateLogger();

            try
            {
                BuildAvaloniaApp()
                    .StartWithClassicDesktopLifetime(args);
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }
        finally
        {
            _mutex.ReleaseMutex();
            _mutex.Dispose();
        }
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
    }
}