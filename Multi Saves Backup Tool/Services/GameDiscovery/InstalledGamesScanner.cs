using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;
using Multi_Saves_Backup_Tool.Models;

namespace Multi_Saves_Backup_Tool.Services.GameDiscovery;

public class InstalledGamesScanner
{
    [SupportedOSPlatform("windows")]
    public async Task<IEnumerable<GameModel>> ScanForInstalledGamesAsync(CancellationToken cancellationToken = default)
    {
        var allGames = new List<GameModel>();

        var tasks = new[]
        {
            ScanSteamGamesAsync(cancellationToken),
            ScanEpicGamesAsync(cancellationToken),
            ScanFromRegistryAsync(cancellationToken)
        };

        var results = await Task.WhenAll(tasks);
        foreach (var games in results)
        foreach (var game in games)
        {
            var savePaths = await GetSavePathsFromPcGamingWikiAsync(game);
            game.SavePath = savePaths.Any() ? savePaths.FirstOrDefault() : string.Empty;
            allGames.Add(game);
        }

        return allGames.DistinctBy(g => g.GameExe);
    }

    [SupportedOSPlatform("windows")]
    private async Task<IEnumerable<GameModel>> ScanSteamGamesAsync(CancellationToken cancellationToken = default)
    {
        var games = new List<GameModel>();
        try
        {
            var steamPath = GetSteamInstallPath();
            if (string.IsNullOrEmpty(steamPath))
                return games;

            var steamAppsPath = Path.Combine(steamPath, "steamapps");
            var libraryFoldersPath = Path.Combine(steamAppsPath, "libraryfolders.vdf");
            if (!File.Exists(libraryFoldersPath))
                return games;

            var libraryContent = await File.ReadAllTextAsync(libraryFoldersPath, cancellationToken);
            var libraryPaths = ParseSteamLibraryPaths(libraryContent);

            foreach (var libraryPath in libraryPaths)
            {
                var acfFiles = Directory.GetFiles(Path.Combine(libraryPath, "steamapps"), "*.acf");
                foreach (var acfFile in acfFiles)
                {
                    var gameInfo = await ParseSteamAcfFileAsync(acfFile, libraryPath, cancellationToken);
                    if (gameInfo != null)
                        games.Add(gameInfo);
                }
            }
        }
        catch (Exception ex)
        {
            LogError("Error scanning Steam games: {0}", ex.Message);
        }

        return games;
    }

    private async Task<IEnumerable<GameModel>> ScanEpicGamesAsync(CancellationToken cancellationToken = default)
    {
        var games = new List<GameModel>();
        try
        {
            var epicManifestsPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "Epic", "EpicGamesLauncher", "Data", "Manifests");
            if (!Directory.Exists(epicManifestsPath))
                return games;

            var manifestFiles = Directory.GetFiles(epicManifestsPath, "*.item");
            foreach (var manifestFile in manifestFiles)
            {
                var gameInfo = await ParseEpicManifestAsync(manifestFile, cancellationToken);
                if (gameInfo != null)
                    games.Add(gameInfo);
            }
        }
        catch (Exception ex)
        {
            LogError("Error scanning Epic Games: {0}", ex.Message);
        }

        return games;
    }

    [SupportedOSPlatform("windows")]
    private async Task<IEnumerable<GameModel>> ScanFromRegistryAsync(CancellationToken cancellationToken = default)
    {
        return await Task.Run(() =>
        {
            var games = new List<GameModel>();
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return games;
            try
            {
                var uninstallKey =
                    Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall");
                if (uninstallKey != null)
                    foreach (var subKeyName in uninstallKey.GetSubKeyNames())
                    {
                        var subKey = uninstallKey.OpenSubKey(subKeyName);
                        if (subKey != null)
                        {
                            var gameInfo = ParseRegistryGameInfo(subKey);
                            if (gameInfo != null)
                                games.Add(gameInfo);
                        }
                    }
            }
            catch (Exception ex)
            {
                LogError("Error scanning registry: {0}", ex.Message);
            }

            return games;
        }, cancellationToken);
    }

    [SupportedOSPlatform("windows")]
    public static string? GetSteamInstallPath()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return null;
        try
        {
            var steamKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\Valve\Steam") ??
                           Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Valve\Steam");
            return steamKey?.GetValue("InstallPath")?.ToString();
        }
        catch
        {
            return null;
        }
    }

    private List<string> ParseSteamLibraryPaths(string vdfContent)
    {
        var paths = new List<string>();
        var lines = vdfContent.Split('\n');
        foreach (var line in lines)
            if (line.Contains("\"path\""))
            {
                var pathMatch = Regex.Match(line, "\\\"path\\\"\\s*\\\"([^\\\"]+)\\\"");
                if (pathMatch.Success)
                    paths.Add(pathMatch.Groups[1].Value.Replace("\\\\", "\\"));
            }

        return paths;
    }

    private async Task<GameModel?> ParseSteamAcfFileAsync(string acfPath, string libraryPath,
        CancellationToken cancellationToken)
    {
        try
        {
            var content = await File.ReadAllTextAsync(acfPath, cancellationToken);
            var nameMatch = Regex.Match(content, "\\\"name\\\"\\s*\\\"([^\\\"]+)\\\"");
            var installDirMatch = Regex.Match(content, "\\\"installdir\\\"\\s*\\\"([^\\\"]+)\\\"");
            if (nameMatch.Success && installDirMatch.Success)
            {
                var gamePath = Path.Combine(libraryPath, "steamapps", "common", installDirMatch.Groups[1].Value);
                var exePath = FindMainExecutable(gamePath);
                return new GameModel
                {
                    GameName = nameMatch.Groups[1].Value,
                    GameExe = exePath,
                    IsEnabled = false
                };
            }
        }
        catch (Exception ex)
        {
            LogError("Error parsing ACF file {0}: {1}", acfPath, ex.Message);
        }

        return null;
    }

    private async Task<GameModel?> ParseEpicManifestAsync(string manifestPath, CancellationToken cancellationToken)
    {
        try
        {
            var content = await File.ReadAllTextAsync(manifestPath, cancellationToken);
            var manifest = JsonSerializer.Deserialize<JsonElement>(content);
            if (manifest.TryGetProperty("DisplayName", out var displayName) &&
                manifest.TryGetProperty("InstallLocation", out var installLocation))
            {
                var gamePath = installLocation.GetString();
                var exePath = FindMainExecutable(gamePath);
                return new GameModel
                {
                    GameName = displayName.GetString() ?? "Unknown",
                    GameExe = exePath,
                    IsEnabled = false
                };
            }
        }
        catch (Exception ex)
        {
            LogError("Error parsing Epic manifest {0}: {1}", manifestPath, ex.Message);
        }

        return null;
    }

    [SupportedOSPlatform("windows")]
    private GameModel? ParseRegistryGameInfo(RegistryKey key)
    {
        try
        {
            var displayName = key.GetValue("DisplayName")?.ToString();
            var installLocation = key.GetValue("InstallLocation")?.ToString();
            var publisher = key.GetValue("Publisher")?.ToString();
            if (string.IsNullOrEmpty(displayName) || string.IsNullOrEmpty(installLocation))
                return null;
            var gameKeywords = new[] { "game", "games", "steam", "epic", "ubisoft", "ea", "activision" };
            var isLikelyGame = gameKeywords.Any(keyword =>
                displayName.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                (publisher?.Contains(keyword, StringComparison.OrdinalIgnoreCase) ?? false));
            if (!isLikelyGame)
                return null;
            var exePath = FindMainExecutable(installLocation);
            return new GameModel
            {
                GameName = displayName,
                GameExe = exePath,
                IsEnabled = false
            };
        }
        catch (Exception ex)
        {
            LogError("Error parsing registry game info: {0}", ex.Message);
            return null;
        }
    }

    private string FindMainExecutable(string? gamePath)
    {
        if (string.IsNullOrEmpty(gamePath) || !Directory.Exists(gamePath))
            return string.Empty;
        try
        {
            var exeFiles = Directory.GetFiles(gamePath, "*.exe", SearchOption.AllDirectories)
                .Where(f => !Path.GetFileName(f).StartsWith("unins", StringComparison.OrdinalIgnoreCase))
                .Where(f => !Path.GetFileName(f).Contains("crash", StringComparison.OrdinalIgnoreCase))
                .Where(f => !Path.GetFileName(f).Contains("setup", StringComparison.OrdinalIgnoreCase))
                .OrderBy(f => Path.GetDirectoryName(f) == gamePath ? 0 : 1)
                .ThenByDescending(f => new FileInfo(f).Length)
                .ToList();
            return exeFiles.FirstOrDefault() ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private void LogError(string message, params object[] args)
    {
        Debug.WriteLine(message, args);
    }

    [SupportedOSPlatform("windows")]
    private async Task<List<string>> GetSavePathsFromPcGamingWikiAsync(GameModel game)
    {
        var result = await PcGamingWikiSaveParser.GetWindowsSavePathsAsync(game);
        if (result.Found)
            return result.WindowsPaths;

        return new List<string>();
    }
}