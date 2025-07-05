using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Runtime.Versioning;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
using Multi_Saves_Backup_Tool.Models;
using Properties;

namespace Multi_Saves_Backup_Tool.Services.GameDiscovery;

public static class PcGamingWikiSaveParser
{
    private const string WikiApiUrl = "https://www.pcgamingwiki.com/w/api.php";
    private static readonly HttpClient HttpClient = new();

    [SupportedOSPlatform("windows")]
    public static async Task<SavePathResult> GetWindowsSavePathsAsync(GameModel? game)
    {
        try
        {
            var wikitext = await GetWikitextAsync(game?.GameName);
            if (string.IsNullOrEmpty(wikitext))
                return new SavePathResult { ErrorMessage = Resources.PCGamingWikiSaveParser_PageNotFound };

            var windowsPaths = ParseWindowsSavePaths(wikitext, game);
            return new SavePathResult
            {
                WindowsPaths = windowsPaths,
                ErrorMessage = windowsPaths.Count == 0 ? Resources.PCGamingWikiSaveParser_SavePathNotFound : null
            };
        }
        catch (Exception ex)
        {
            return new SavePathResult { ErrorMessage = $"{Resources.PCGamingWikiSaveParser_Error}: {ex.Message}" };
        }
    }

    private static async Task<string> GetWikitextAsync(string? gameName)
    {
        var encodedGameName = HttpUtility.UrlEncode(gameName);
        var url =
            $"{WikiApiUrl}?action=query&format=json&titles={encodedGameName}&prop=revisions&rvprop=content&rvslots=main";
        var response = await HttpClient.GetAsync(url);
        response.EnsureSuccessStatusCode();
        var jsonResponse = await response.Content.ReadAsStringAsync();

        using var doc = JsonDocument.Parse(jsonResponse);
        var query = doc.RootElement.GetProperty("query");
        var pages = query.GetProperty("pages");
        foreach (var page in pages.EnumerateObject())
        {
            var pageValue = page.Value;
            if (pageValue.TryGetProperty("revisions", out var revisions))
            {
                var revision = revisions[0];
                var slots = revision.GetProperty("slots");
                var main = slots.GetProperty("main");
                return main.GetProperty("*").GetString() ?? string.Empty;
            }
        }

        return null!;
    }

    [SupportedOSPlatform("windows")]
    private static List<string> ParseWindowsSavePaths(string wikitext, GameModel? game)
    {
        var result = new List<string>();
        var saveDataMatch = Regex.Match(wikitext, @"===Save game data location===.*?(?=\n===|\n==|\z)",
            RegexOptions.Singleline);
        if (!saveDataMatch.Success)
            return result;

        var saveDataSection = saveDataMatch.Value;

        var matches = Regex.Matches(saveDataSection, @"\{\{Game data/saves\|Windows\|((?:\{\{[^}]*\}\}|[^|}])*)",
            RegexOptions.IgnoreCase);
        foreach (Match match in matches)
        {
            var rawPath = match.Groups[1].Value.Trim();
            var processedPath = ProcessWikiVariables(rawPath, game);
            if (!string.IsNullOrWhiteSpace(processedPath))
                result.Add(processedPath);
        }

        return result;
    }

    [SupportedOSPlatform("windows")]
    private static string ProcessWikiVariables(string path, GameModel? game)
    {
        var variables = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            { "localappdata", Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) },
            { "appdata", Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) },
            { "userprofile", Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) },
            { "programdata", Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData) },
            { "documents", Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) },
            { "game", Path.GetDirectoryName(game?.GameExe.Trim()) },
            { "steam", InstalledGamesScanner.GetSteamInstallPath() },
            {
                "steamuserdata",
                Path.Combine(InstalledGamesScanner.GetSteamInstallPath() ?? string.Empty, "\\userdata\\")
            }
        };

        var wikiTemplateRegex = new Regex(@"\{\{p\|([^}|]+)(?:\|([^}]+))?\}\}", RegexOptions.IgnoreCase);

        var processedPath = path;
        Match match;
        while ((match = wikiTemplateRegex.Match(processedPath)).Success)
        {
            var variableName = match.Groups[1].Value;

            var variableParts = variableName.Split(['\\', '/'], StringSplitOptions.RemoveEmptyEntries);
            string? fullPath;

            if (variableParts.Length > 0)
            {
                if (variables.TryGetValue(variableParts[0].ToLower(), out var basePath))
                {
                    fullPath = basePath;
                    for (var i = 1; i < variableParts.Length; i++)
                        if (fullPath != null)
                            fullPath = Path.Combine(fullPath, variableParts[i]);
                }
                else
                {
                    fullPath = $"[Unknown Variable: {variableName}]";
                }

                if (match.Groups.Count > 2 && !string.IsNullOrEmpty(match.Groups[2].Value))
                    if (fullPath != null)
                        fullPath = Path.Combine(fullPath, match.Groups[2].Value);
            }
            else
            {
                fullPath = $"[Unknown Variable: {variableName}]";
            }

            processedPath = processedPath.Replace(match.Value, fullPath);
        }

        processedPath = Regex.Replace(processedPath, @"\{\{[^}]+\}\}", "");
        processedPath = processedPath.Trim().Trim(Path.DirectorySeparatorChar);

        if (!string.IsNullOrEmpty(Path.GetExtension(processedPath)))
            return Path.GetDirectoryName(processedPath) ?? processedPath;

        return processedPath;
    }

    public class SavePathResult
    {
        public List<string> WindowsPaths { get; set; } = new();
        public bool Found => WindowsPaths.Count > 0;
        public string? ErrorMessage { get; set; }
    }
}