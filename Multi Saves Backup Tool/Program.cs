using System;
using System.IO;
using System.Threading;
using Avalonia;
using Multi_Saves_Backup_Tool.Paths;
using Serilog;

namespace Multi_Saves_Backup_Tool;

internal static class Program
{
    private static Mutex? _mutex;
    private static readonly TimeSpan LogRetention = TimeSpan.FromDays(3);

    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        const string appName = "MultiSavesBackupTool";
        _mutex = new Mutex(true, appName, out var createdNew);

        if (!createdNew) return;

        try
        {
            var dataAppDir = AppPaths.DataPath;

            CleanupOldLogs(dataAppDir);

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
    private static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
    }

    private static void CleanupOldLogs(string directory)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
                return;

            var cutoff = DateTime.UtcNow - LogRetention;
            var files = Directory.GetFiles(directory, "backup_service*.log", SearchOption.TopDirectoryOnly);

            foreach (var file in files)
                try
                {
                    var lastWrite = File.GetLastWriteTimeUtc(file);
                    if (lastWrite < cutoff) File.Delete(file);
                }
                catch
                {
                    // Ignore individual file errors
                }
        }
        catch
        {
            // Ignore cleanup errors
        }
    }
}