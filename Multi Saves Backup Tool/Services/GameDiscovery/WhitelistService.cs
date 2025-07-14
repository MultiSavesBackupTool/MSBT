using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
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
        _httpClient = httpClient ?? new HttpClient();
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
            var serverWhitelist = JsonSerializer.Deserialize<string[][]>(response) ?? [];

            var newEntries = new List<WhitelistEntry>();

            foreach (var entry in serverWhitelist)
                if (entry.Length >= 6)
                {
                    var whitelistEntry = new WhitelistEntry(
                        entry[0],
                        entry[1],
                        entry.Length > 2 && !string.IsNullOrEmpty(entry[2]) ? entry[2] : null,
                        entry[3],
                        entry.Length > 4 && !string.IsNullOrEmpty(entry[4]) ? entry[4] : null,
                        entry.Length > 5 && !string.IsNullOrEmpty(entry[5]) ? entry[5] : null,
                        entry.Length > 6 && int.TryParse(entry[6], out var specialMark) && specialMark == 1
                    );

                    if (_whitelist.TryAdd(whitelistEntry.GameName, whitelistEntry))
                    {
                        newEntries.Add(whitelistEntry);
                    }
                }

            if (newEntries.Count > 0)
            {
                await SaveWhitelistAsync();
                _logger.LogInformation("Added {Count} new entries from server to whitelist", newEntries.Count);
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
            _logger.LogInformation("Contributing {GameName} to server whitelist", entry.GameName);
            var data = new
            {
                gameName = entry.GameName,
                gameExe = entry.GameExe,
                gameExeAlt = entry.GameExeAlt,
                savePath = entry.SavePath,
                modPath = entry.ModPath,
                addPath = entry.AddPath,
                specialBackupMark = entry.SpecialBackupMark ? 1 : 0
            };

            var json = JsonSerializer.Serialize(data);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(ServerUrl, content);
            if (response.IsSuccessStatusCode)
                _logger.LogInformation("Successfully contributed {GameName} to server", entry.GameName);
            else
                _logger.LogWarning("Failed to contribute {GameName} to server. Status: {StatusCode}",
                    entry.GameName, response.StatusCode);
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
}