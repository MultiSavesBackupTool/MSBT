using System;
using System.IO;

namespace Multi_Saves_Backup_Tool.Paths;

public static class AppPaths
{
    private static string? _configPath;
    private static string? _dataPath;

    public static string ConfigPath
    {
        get
        {
            if (_configPath != null) return _configPath;

            if (OperatingSystem.IsWindows())
                _configPath = Path.GetDirectoryName(AppContext.BaseDirectory) ?? "";
            else if (OperatingSystem.IsMacOS())
                _configPath = MacOsPaths.GetApplicationSupportPath();
            else if (OperatingSystem.IsLinux())
                _configPath = LinuxPaths.GetConfigPath();
            else
                _configPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

            Directory.CreateDirectory(_configPath);
            return _configPath;
        }
    }

    public static string DataPath
    {
        get
        {
            if (_dataPath != null) return _dataPath;

            if (OperatingSystem.IsWindows())
                _dataPath = Path.GetDirectoryName(AppContext.BaseDirectory) ?? "";
            else if (OperatingSystem.IsMacOS())
                _dataPath = MacOsPaths.GetDocumentsPath();
            else if (OperatingSystem.IsLinux())
                _dataPath = LinuxPaths.GetDataPath();
            else
                _dataPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

            Directory.CreateDirectory(_dataPath);
            return _dataPath;
        }
    }

    public static string SettingsFilePath => Path.Combine(ConfigPath, "settings.json");
    public static string GamesFilePath => Path.Combine(DataPath, "games.json");
    public static string ServiceStateFilePath => Path.Combine(DataPath, "backup_state.json");

    private static class MacOsPaths
    {
        public static string GetApplicationSupportPath()
        {
            var userHome = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.Combine(userHome, "Library", "Application Support", "MSBT");
        }

        public static string GetDocumentsPath()
        {
            var documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            return Path.Combine(documentsPath, "MSBT");
        }

        public static string GetCachePath()
        {
            var userHome = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.Combine(userHome, "Library", "Caches", "MSBT");
        }
    }

    public static class LinuxPaths
    {
        public static string GetConfigPath()
        {
            var xdgConfig = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
            if (!string.IsNullOrEmpty(xdgConfig))
                return Path.Combine(xdgConfig, "msbt");

            var userHome = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.Combine(userHome, ".config", "msbt");
        }

        public static string GetDataPath()
        {
            var xdgData = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
            if (!string.IsNullOrEmpty(xdgData))
                return Path.Combine(xdgData, "msbt");

            var userHome = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.Combine(userHome, ".local", "share", "msbt");
        }

        public static string GetCachePath()
        {
            var xdgCache = Environment.GetEnvironmentVariable("XDG_CACHE_HOME");
            if (!string.IsNullOrEmpty(xdgCache))
                return Path.Combine(xdgCache, "msbt");

            var userHome = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.Combine(userHome, ".cache", "msbt");
        }
    }
}