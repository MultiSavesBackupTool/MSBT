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
using Multi_Saves_Backup_Tool.Paths;

namespace Multi_Saves_Backup_Tool.Services.GameDiscovery;

public class BlacklistService : IBlacklistService
{
    private const string ServerUrl = "https://msbt.lukiuwu.xyz/api/blacklist";
    private readonly string _blacklistPath;
    private readonly HttpClient _httpClient;
    private readonly ILogger<BlacklistService> _logger;
    private HashSet<string> _blacklist;

    public BlacklistService(ILogger<BlacklistService> logger, HttpClient? httpClient = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _httpClient = httpClient ?? new HttpClient(new HttpClientHandler
        {
            SslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13
        });
        _blacklistPath = AppPaths.BlacklistFilePath;
        _blacklist = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        _ = LoadBlacklistAsync();
    }

    public async Task<IEnumerable<string>> GetBlacklistAsync()
    {
        if (_blacklist.Count == 0) await LoadBlacklistAsync();
        return _blacklist.ToList();
    }

    public async Task AddToBlacklistAsync(string gameName)
    {
        if (string.IsNullOrWhiteSpace(gameName))
            return;

        if (_blacklist.Add(gameName))
        {
            await SaveBlacklistAsync();
            _logger.LogInformation("Added {GameName} to blacklist", gameName);
        }
    }

    public async Task RemoveFromBlacklistAsync(string gameName)
    {
        if (string.IsNullOrWhiteSpace(gameName))
            return;

        if (_blacklist.Remove(gameName))
        {
            await SaveBlacklistAsync();
            _logger.LogInformation("Removed {GameName} from blacklist", gameName);
        }
    }

    public async Task SyncWithServerAsync()
    {
        try
        {
            _logger.LogInformation("Syncing blacklist with server...");
            var response = await _httpClient.GetStringAsync(ServerUrl);
            var serverBlacklist = JsonSerializer.Deserialize<string[][]>(response) ?? [];

            var serverEntries = serverBlacklist
                .Where(entry => entry.Length > 0)
                .Select(entry => entry[0])
                .Where(entry => !string.IsNullOrWhiteSpace(entry))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var newEntries = serverEntries.Except(_blacklist, StringComparer.OrdinalIgnoreCase).ToList();

            foreach (var entry in newEntries) _blacklist.Add(entry);

            if (newEntries.Count > 0)
            {
                await SaveBlacklistAsync();
                _logger.LogInformation("Added {Count} new entries from server to blacklist", newEntries.Count);
            }
            else
            {
                _logger.LogInformation("No new entries to add from server to blacklist.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to sync blacklist with server");
        }
    }

    public async Task ContributeToServerAsync(string gameName)
    {
        if (string.IsNullOrWhiteSpace(gameName))
            return;

        try
        {
            var response = await _httpClient.GetStringAsync(ServerUrl);
            var serverBlacklist = JsonSerializer.Deserialize<object[][]>(response) ?? [];
            
            bool exists = serverBlacklist.Any(e =>
                e.Length > 0 && string.Equals(e[0]?.ToString(), gameName, StringComparison.OrdinalIgnoreCase));

            if (exists)
            {
                _logger.LogInformation("Game {GameName} already exists on the server blacklist. Skipping contribution.", gameName);
                return;
            }

            _logger.LogInformation("Contributing {GameName} to server blacklist", gameName);
            var data = new { gameName };
            var json = JsonSerializer.Serialize(data);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var postResponse = await _httpClient.PostAsync(ServerUrl, content);
            if (postResponse.IsSuccessStatusCode)
                _logger.LogInformation("Successfully contributed {GameName} to server", gameName);
            else
                _logger.LogWarning("Failed to contribute {GameName} to server. Status: {StatusCode}",
                    gameName, postResponse.StatusCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error contributing {GameName} to server", gameName);
        }
    }

    public bool IsBlacklisted(string gameName)
    {
        if (string.IsNullOrWhiteSpace(gameName))
            return false;

        return _blacklist.Contains(gameName);
    }

    public int GetBlacklistCount()
    {
        return _blacklist.Count;
    }

    private async Task LoadBlacklistAsync()
    {
        try
        {
            if (!File.Exists(_blacklistPath))
            {
                _blacklist = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var directory = Path.GetDirectoryName(_blacklistPath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                    Directory.CreateDirectory(directory);
                await File.WriteAllTextAsync(_blacklistPath, "[]");
                _logger.LogInformation("Created empty blacklist file at {Path}", _blacklistPath);
                return;
            }

            var json = await File.ReadAllTextAsync(_blacklistPath);
            var blacklist = JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
            _blacklist = new HashSet<string>(blacklist, StringComparer.OrdinalIgnoreCase);

            _logger.LogInformation("Loaded {Count} blacklist entries", _blacklist.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading blacklist from {Path}", _blacklistPath);
            _blacklist = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private async Task SaveBlacklistAsync()
    {
        try
        {
            var directory = Path.GetDirectoryName(_blacklistPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory)) Directory.CreateDirectory(directory);

            var json = JsonSerializer.Serialize(_blacklist.ToList(),
                new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(_blacklistPath, json);

            _logger.LogDebug("Saved {Count} blacklist entries to {Path}", _blacklist.Count, _blacklistPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving blacklist to {Path}", _blacklistPath);
        }
    }
}