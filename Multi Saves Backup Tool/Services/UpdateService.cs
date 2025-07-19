using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using FluentAvalonia.UI.Controls;
using Properties;

namespace Multi_Saves_Backup_Tool.Services;

public class UpdateService : IDisposable
{
    private const string GithubApiUrl = "https://api.github.com/repos/{owner}/{repo}/releases/latest";
    private const string Owner = "MultiSavesBackupTool";
    private const string Repo = "MSBT";
    private readonly string _currentVersion;
    private readonly HttpClient _httpClient;
    private bool _disposed;

    public UpdateService()
    {
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "MultiSavesBackupToolUpdateChecker");
        _currentVersion = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0.0";
        Debug.WriteLine($"UpdateService initialized. Current version: {_currentVersion}");
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    public async Task<(bool hasUpdate, string latestVersion, string? downloadUrl)> CheckForUpdatesAsync()
    {
        ThrowIfDisposed();

        try
        {
            var apiUrl = GithubApiUrl.Replace("{owner}", Owner).Replace("{repo}", Repo);
            Debug.WriteLine($"Checking for updates at: {apiUrl}");

            var response = await _httpClient.GetStringAsync(apiUrl);
            Debug.WriteLine($"Received response from GitHub API: {response}");

            try
            {
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };
                var release = JsonSerializer.Deserialize<GitHubRelease>(response, options);

                if (release == null || string.IsNullOrEmpty(release.TagName))
                {
                    Debug.WriteLine("Failed to deserialize release or tag name is empty");
                    return (false, _currentVersion, null);
                }

                Debug.WriteLine($"Successfully deserialized release. Tag: {release.TagName}, Name: {release.Name}");

                var latestVersionString = release.TagName.TrimStart('v');
                Debug.WriteLine($"Current version: {_currentVersion}, Latest version: {latestVersionString}");

                // Try custom version format first, then fallback to standard semantic versioning
                var hasUpdate = false;
                if (IsCustomVersionFormat(latestVersionString) && IsCustomVersionFormat(_currentVersion))
                {
                    hasUpdate = CompareCustomVersions(latestVersionString, _currentVersion) > 0;
                    Debug.WriteLine($"Using custom version comparison. Has update: {hasUpdate}");
                }
                else
                {
                    // Fallback to standard semantic versioning
                    if (!latestVersionString.Contains('.'))
                        latestVersionString = $"{latestVersionString}.0.0.0";

                    if (!Version.TryParse(latestVersionString, out var latestVersionParsed) ||
                        !Version.TryParse(_currentVersion, out var currentVersionParsed))
                    {
                        Debug.WriteLine(
                            $"Could not parse versions for comparison: '{latestVersionString}' and '{_currentVersion}'");
                        hasUpdate = string.Compare(latestVersionString, _currentVersion,
                            StringComparison.OrdinalIgnoreCase) > 0;
                    }
                    else
                    {
                        hasUpdate = latestVersionParsed.CompareTo(currentVersionParsed) > 0;
                    }
                    Debug.WriteLine($"Using standard version comparison. Has update: {hasUpdate}");
                }

                string? downloadUrl = null;
                if (hasUpdate)
                {
                    downloadUrl = GetDownloadUrlForCurrentPlatform(release.Assets);
                    Debug.WriteLine($"Update found. Has update: {hasUpdate}, Download URL: {downloadUrl}");
                }

                if (hasUpdate && string.IsNullOrEmpty(downloadUrl))
                    Debug.WriteLine(
                        $"Update detected (v{latestVersionString}) but no suitable download URL found for current platform.");

                return (hasUpdate, latestVersionString, downloadUrl);
            }
            catch (JsonException ex)
            {
                Debug.WriteLine($"JSON deserialization error checking for updates: {ex.Message}");
                return (false, _currentVersion, null);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error checking for updates: {ex.Message}");
            return (false, _currentVersion, null);
        }
    }

    private bool IsCustomVersionFormat(string version)
    {
        if (string.IsNullOrWhiteSpace(version))
            return false;

        // Pattern: YY.S.M.B where YY=year, S=season (P/S/A/W), M=minor, B=patch
        var pattern = @"^(\d{2})\.([PSAW])\.(\d+)\.(\d+)$";
        return Regex.IsMatch(version, pattern, RegexOptions.IgnoreCase);
    }

    private int CompareCustomVersions(string version1, string version2)
    {
        try
        {
            var match1 = Regex.Match(version1, @"^(\d{2})\.([PSAW])\.(\d+)\.(\d+)$", RegexOptions.IgnoreCase);
            var match2 = Regex.Match(version2, @"^(\d{2})\.([PSAW])\.(\d+)\.(\d+)$", RegexOptions.IgnoreCase);

            if (!match1.Success || !match2.Success)
                return 0;

            // Extract components
            var year1 = int.Parse(match1.Groups[1].Value);
            var year2 = int.Parse(match2.Groups[1].Value);
            
            var season1 = match1.Groups[2].Value.ToUpper();
            var season2 = match2.Groups[2].Value.ToUpper();
            
            var minor1 = int.Parse(match1.Groups[3].Value);
            var minor2 = int.Parse(match2.Groups[3].Value);
            
            var patch1 = int.Parse(match1.Groups[4].Value);
            var patch2 = int.Parse(match2.Groups[4].Value);

            // Compare year first
            if (year1 != year2)
                return year1.CompareTo(year2);

            // Compare season (P < S < A < W)
            var seasonOrder = new Dictionary<string, int> { { "P", 0 }, { "S", 1 }, { "A", 2 }, { "W", 3 } };
            var season1Order = seasonOrder[season1];
            var season2Order = seasonOrder[season2];
            
            if (season1Order != season2Order)
                return season1Order.CompareTo(season2Order);

            // Compare minor release
            if (minor1 != minor2)
                return minor1.CompareTo(minor2);

            // Compare patch
            return patch1.CompareTo(patch2);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error comparing custom versions '{version1}' and '{version2}': {ex.Message}");
            return 0;
        }
    }

    private string? GetDownloadUrlForCurrentPlatform(List<GitHubAsset>? assets)
    {
        if (assets == null || !assets.Any())
            return null;

        if (OperatingSystem.IsWindows())
            return assets.FirstOrDefault(a => a.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                ?.BrowserDownloadUrl;

        return assets.FirstOrDefault()?.BrowserDownloadUrl;
    }

    public async Task<bool> DownloadAndInstallUpdateAsync(string downloadUrl)
    {
        ThrowIfDisposed();

        if (string.IsNullOrWhiteSpace(downloadUrl))
        {
            Debug.WriteLine("Download URL is empty. Cannot download update.");
            return false;
        }

        ContentDialog? dialog = null;
        try
        {
            dialog = new ContentDialog
            {
                Title = Resources.UpdateWarningTitle,
                Content = new StackPanel
                {
                    Spacing = 8,
                    Children =
                    {
                        new TextBlock
                        {
                            Text = Resources.UpdateWarningContent,
                            FontSize = 14
                        },
                        new ProgressBar
                        {
                            IsIndeterminate = true,
                            Height = 16
                        }
                    }
                }
            };

            var fileName = Path.GetFileName(new Uri(downloadUrl).AbsolutePath);
            var tempFile = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}_{fileName}");

            Debug.WriteLine($"Downloading update from {downloadUrl} to {tempFile}");

            _ = dialog.ShowAsync();

            var response = await _httpClient.GetAsync(downloadUrl);
            response.EnsureSuccessStatusCode();
            Debug.WriteLine($"Download response status: {response.StatusCode}");

            await using (var fileStream = File.Create(tempFile))
            {
                await response.Content.CopyToAsync(fileStream);
            }

            Debug.WriteLine($"Download complete: {tempFile}");

            if (OperatingSystem.IsWindows())
                return await InstallUpdateOnWindows(tempFile);

            dialog.Hide();
            return false;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error downloading or installing update: {ex.Message}");
            dialog?.Hide();
            return false;
        }
    }

    private Task<bool> InstallUpdateOnWindows(string filePath)
    {
        try
        {
            var processStartInfo = new ProcessStartInfo(filePath)
            {
                FileName = filePath,
                Arguments = "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /CLOSEAPPLICATIONS",
                UseShellExecute = true,
                Verb = "runas",
                CreateNoWindow = true
            };
            Process.Start(processStartInfo);

            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime lifetime)
                lifetime.Shutdown();

            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error starting Windows update installer: {ex.Message}");
            return Task.FromResult(false);
        }
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing) _httpClient.Dispose();
            _disposed = true;
        }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(UpdateService));
    }
}

public class GitHubRelease
{
    [JsonPropertyName("tag_name")] public string TagName { get; set; } = string.Empty;
    [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
    [JsonPropertyName("body")] public string Body { get; set; } = string.Empty;
    [JsonPropertyName("assets")] public List<GitHubAsset>? Assets { get; set; }
    [JsonPropertyName("html_url")] public string HtmlUrl { get; set; } = string.Empty;
    [JsonPropertyName("target_commitish")] public string TargetCommitish { get; set; } = string.Empty;
    [JsonPropertyName("draft")] public bool Draft { get; set; }
    [JsonPropertyName("prerelease")] public bool Prerelease { get; set; }
    [JsonPropertyName("created_at")] public DateTime CreatedAt { get; set; }
    [JsonPropertyName("published_at")] public DateTime PublishedAt { get; set; }
}

public class GitHubAsset
{
    [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;

    [JsonPropertyName("browser_download_url")]
    public string BrowserDownloadUrl { get; set; } = string.Empty;

    [JsonPropertyName("content_type")] public string ContentType { get; set; } = string.Empty;
    [JsonPropertyName("size")] public long Size { get; set; }
    [JsonPropertyName("download_count")] public int DownloadCount { get; set; }
    [JsonPropertyName("created_at")] public DateTime CreatedAt { get; set; }
    [JsonPropertyName("updated_at")] public DateTime UpdatedAt { get; set; }
}