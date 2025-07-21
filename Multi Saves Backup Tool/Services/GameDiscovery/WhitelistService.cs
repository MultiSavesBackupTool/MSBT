using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Authentication;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Multi_Saves_Backup_Tool.Models;
using Multi_Saves_Backup_Tool.Paths;

namespace Multi_Saves_Backup_Tool.Services.GameDiscovery;

public class WhitelistService : IWhitelistService
{
    private const string ServerUrl = "https://msbt.lukiuwu.xyz/api/whitelist";
    private readonly HttpClient _httpClient;
    private readonly ILogger<WhitelistService> _logger;
    private readonly string _whitelistPath;
    private Dictionary<string, WhitelistEntry> _whitelist;

    public WhitelistService(ILogger<WhitelistService> logger, HttpClient? httpClient = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _httpClient = httpClient ?? new HttpClient(new HttpClientHandler
        {
            SslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13
        });
        _whitelistPath = AppPaths.WhitelistFilePath;
        _whitelist = new Dictionary<string, WhitelistEntry>(StringComparer.OrdinalIgnoreCase);

        _ = LoadWhitelistAsync();
    }

    public async Task<IEnumerable<WhitelistEntry>> GetWhitelistAsync()
    {
        if (_whitelist.Count == 0) await LoadWhitelistAsync();
        return _whitelist.Values.ToList();
    }

    public async Task AddToWhitelistAsync(WhitelistEntry entry)
    {
        if (string.IsNullOrWhiteSpace(entry.GameName))
            return;

        if (_whitelist.TryAdd(entry.GameName, entry))
        {
            await SaveWhitelistAsync();
            _logger.LogInformation("Added {GameName} to whitelist", entry.GameName);
        }
    }

    public async Task RemoveFromWhitelistAsync(string gameName)
    {
        if (string.IsNullOrWhiteSpace(gameName))
            return;

        if (_whitelist.Remove(gameName))
        {
            await SaveWhitelistAsync();
            _logger.LogInformation("Removed {GameName} from whitelist", gameName);
        }
    }

    public async Task SyncWithServerAsync()
    {
        try
        {
            _logger.LogInformation("Syncing whitelist with server...");
            var response = await _httpClient.GetStringAsync(ServerUrl);
            var serverWhitelist = JsonSerializer.Deserialize<object[][]>(response) ?? [];

            var newEntries = new List<WhitelistEntry>();

            foreach (var entry in serverWhitelist)
                if (entry.Length >= 4)
                {
                    var gameName = entry[0].ToString() ?? string.Empty;
                    var savePath = entry[1].ToString() ?? string.Empty;
                    var modPath = entry.Length > 2 ? entry[2].ToString() : null;
                    var addPath = entry.Length > 3 ? entry[3].ToString() : null;
                    var specialMark = false;
                    if (entry.Length > 4)
                    {
                        if (entry[4] is JsonElement { ValueKind: JsonValueKind.Number } je)
                            specialMark = je.GetInt32() == 1;
                        else if (entry[4] is int i)
                            specialMark = i == 1;
                        else if (entry[4].ToString() == "1")
                            specialMark = true;
                    }

                    var whitelistEntry = new WhitelistEntry(
                        gameName,
                        savePath,
                        modPath,
                        addPath,
                        specialMark
                    );

                    if (_whitelist.TryAdd(whitelistEntry.GameName, whitelistEntry)) newEntries.Add(whitelistEntry);
                }

            if (newEntries.Count > 0)
            {
                await SaveWhitelistAsync();
                _logger.LogInformation("Added {Count} new entries from server to whitelist", newEntries.Count);
            }
            else
            {
                _logger.LogInformation("No new entries to add from server to whitelist.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to sync whitelist with server");
        }
    }

    public async Task ContributeToServerAsync(WhitelistEntry entry)
    {
        if (string.IsNullOrWhiteSpace(entry.GameName))
            return;

        try
        {
            var response = await _httpClient.GetStringAsync(ServerUrl);
            var serverWhitelist = JsonSerializer.Deserialize<object[][]>(response) ?? [];

            var exists = serverWhitelist.Any(e =>
                e.Length > 0 && string.Equals(e[0].ToString(), entry.GameName, StringComparison.OrdinalIgnoreCase));

            if (exists)
            {
                _logger.LogInformation("Game {GameName} already exists on the server whitelist. Skipping contribution.",
                    entry.GameName);
                return;
            }

            _logger.LogInformation("Contributing {GameName} to server whitelist", entry.GameName);
            var data = new
            {
                gameName = entry.GameName,
                savePath = NormalizeUserPath(entry.SavePath),
                modPath = NormalizeUserPath(entry.ModPath),
                addPath = NormalizeUserPath(entry.AddPath),
                specialBackupMark = entry.SpecialBackupMark ? 1 : 0
            };

            var json = JsonSerializer.Serialize(data);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var postResponse = await _httpClient.PostAsync(ServerUrl, content);
            if (postResponse.IsSuccessStatusCode)
                _logger.LogInformation("Successfully contributed {GameName} to server", entry.GameName);
            else
                _logger.LogWarning("Failed to contribute {GameName} to server. Status: {StatusCode}",
                    entry.GameName, postResponse.StatusCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error contributing {GameName} to server", entry.GameName);
        }
    }

    public bool IsWhitelisted(string gameName)
    {
        if (string.IsNullOrWhiteSpace(gameName))
            return false;

        return _whitelist.ContainsKey(gameName);
    }

    public WhitelistEntry? GetWhitelistEntry(string gameName)
    {
        if (string.IsNullOrWhiteSpace(gameName))
            return null;

        return _whitelist.GetValueOrDefault(gameName);
    }

    public int GetWhitelistCount()
    {
        return _whitelist.Count;
    }

    private async Task LoadWhitelistAsync()
    {
        try
        {
            if (!File.Exists(_whitelistPath))
            {
                _whitelist = new Dictionary<string, WhitelistEntry>(StringComparer.OrdinalIgnoreCase);
                var directory = Path.GetDirectoryName(_whitelistPath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                    Directory.CreateDirectory(directory);
                await File.WriteAllTextAsync(_whitelistPath, "[]");
                _logger.LogInformation("Created empty whitelist file at {Path}", _whitelistPath);
                return;
            }

            var json = await File.ReadAllTextAsync(_whitelistPath);
            var whitelist = JsonSerializer.Deserialize<List<WhitelistEntry>>(json) ?? new List<WhitelistEntry>();
            _whitelist = whitelist.ToDictionary(e => e.GameName, e => e, StringComparer.OrdinalIgnoreCase);

            _logger.LogInformation("Loaded {Count} whitelist entries", _whitelist.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading whitelist from {Path}", _whitelistPath);
            _whitelist = new Dictionary<string, WhitelistEntry>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private async Task SaveWhitelistAsync()
    {
        try
        {
            var directory = Path.GetDirectoryName(_whitelistPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory)) Directory.CreateDirectory(directory);

            var json = JsonSerializer.Serialize(_whitelist.Values.ToList(),
                new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(_whitelistPath, json);

            _logger.LogDebug("Saved {Count} whitelist entries to {Path}", _whitelist.Count, _whitelistPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving whitelist to {Path}", _whitelistPath);
        }
    }

    private static string NormalizeUserPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return path ?? string.Empty;

        var specialFolders = new Dictionary<string, string>
        {
            { Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "%appdata%" },
            { Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "%localappdata%" },
            { Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "%userprofile%" },
            { Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "%documents%" },
            { Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "%programdata%" }
        };

        foreach (var kvp in specialFolders)
            if (path.StartsWith(kvp.Key, StringComparison.OrdinalIgnoreCase))
                return path.Replace(kvp.Key, kvp.Value, StringComparison.OrdinalIgnoreCase);

        return path;
    }
}