using System;
using System.IO;

namespace Multi_Saves_Backup_Tool.Paths;

public static class AppPaths
{
    private static string? _configPath;
    private static string? _dataPath;

    private static string ConfigPath
    {
        get
        {
            if (_configPath != null) return _configPath;

            if (OperatingSystem.IsWindows())
                _configPath = Path.GetDirectoryName(AppContext.BaseDirectory) ?? "";
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
            else
                _dataPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

            Directory.CreateDirectory(_dataPath);
            return _dataPath;
        }
    }

    public static string SettingsFilePath => Path.Combine(ConfigPath, "settings.json");
    public static string GamesFilePath => Path.Combine(DataPath, "games.json");
    public static string ServiceStateFilePath => Path.Combine(DataPath, "backup_state.json");
    public static string BlacklistFilePath => Path.Combine(ConfigPath, "blacklist.json");
    public static string WhitelistFilePath => Path.Combine(ConfigPath, "whitelist.json");
}