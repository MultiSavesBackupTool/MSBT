using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Avalonia.Controls;
using FluentAvalonia.UI.Controls;
using Microsoft.Win32;
using Properties;

namespace Multi_Saves_Backup_Tool.Services;

public class UpdateService
{
    private const string GithubApiUrl = "https://api.github.com/repos/{owner}/{repo}/releases/latest";
    private const string Owner = "TheNightlyGod";
    private const string Repo = "MSBT";
    private readonly string _currentVersion;

    private readonly HttpClient _httpClient;

    public UpdateService()
    {
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "MultiSavesBackupToolUpdateChecker");
        _currentVersion = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0.0";
        Debug.WriteLine($"UpdateService initialized. Current version: {_currentVersion}");
    }

    private bool IsDotNetRuntimeInstalled()
    {
        try
        {
            if (!OperatingSystem.IsWindows())
            {
                Debug.WriteLine("Not running on Windows, skipping .NET Runtime check");
                return true;
            }

            using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full");
            if (key != null)
            {
                var release = key.GetValue("Release") as int?;
                Debug.WriteLine($"Found .NET Framework release: {release}");
                return release.HasValue && release.Value >= 528040;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error checking .NET Runtime: {ex.Message}");
        }

        return false;
    }

    public async Task<(bool hasUpdate, string latestVersion, string? downloadUrl)> CheckForUpdatesAsync()
    {
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

                if (release == null)
                {
                    Debug.WriteLine("Failed to deserialize release - release object is null");
                    return (false, _currentVersion, null);
                }

                if (string.IsNullOrEmpty(release.TagName))
                {
                    Debug.WriteLine("Failed to deserialize release - tag name is empty");
                    return (false, _currentVersion, null);
                }

                Debug.WriteLine($"Successfully deserialized release. Tag: {release.TagName}, Name: {release.Name}");

                var latestVersionString = release.TagName.TrimStart('v');
                Debug.WriteLine($"Current version: {_currentVersion}, Latest version: {latestVersionString}");

                if (!latestVersionString.Contains('.')) latestVersionString = $"{latestVersionString}.0.0.0";

                if (!Version.TryParse(latestVersionString, out var latestVersionParsed) ||
                    !Version.TryParse(_currentVersion, out var currentVersionParsed))
                {
                    Debug.WriteLine(
                        $"Could not parse versions for comparison: '{latestVersionString}' and '{_currentVersion}'");
                    var hasUpdateFallback = string.Compare(latestVersionString, _currentVersion,
                        StringComparison.OrdinalIgnoreCase) > 0;
                    var downloadUrlFallback =
                        release.Assets?.FirstOrDefault(a => a.Name.EndsWith(".exe"))?.BrowserDownloadUrl;
                    Debug.WriteLine(
                        $"Using fallback comparison. Has update: {hasUpdateFallback}, Download URL: {downloadUrlFallback}");
                    return (hasUpdateFallback, latestVersionString, downloadUrlFallback);
                }

                var hasUpdate = latestVersionParsed.CompareTo(currentVersionParsed) > 0;
                Debug.WriteLine(
                    $"Version comparison: Current={currentVersionParsed}, Latest={latestVersionParsed}, HasUpdate={hasUpdate}");
                string? downloadUrl = null;
                if (hasUpdate)
                {
                    downloadUrl = release.Assets
                                      ?.FirstOrDefault(a => a.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                                      ?.BrowserDownloadUrl
                                  ?? release.Assets?.FirstOrDefault()?.BrowserDownloadUrl;
                    Debug.WriteLine($"Update found. Has update: {hasUpdate}, Download URL: {downloadUrl}");
                }

                if (hasUpdate && string.IsNullOrEmpty(downloadUrl))
                    Debug.WriteLine(
                        $"Update detected (v{latestVersionString}) but no suitable download URL found in assets.");

                return (hasUpdate, latestVersionString, downloadUrl);
            }
            catch (JsonException ex)
            {
                Debug.WriteLine($"JSON deserialization error checking for updates: {ex.Message}");
                Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                return (false, _currentVersion, null);
            }
        }
        catch (HttpRequestException ex)
        {
            Debug.WriteLine($"HTTP request error checking for updates: {ex.Message}");
            Debug.WriteLine($"Stack trace: {ex.StackTrace}");
            return (false, _currentVersion, null);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Generic error checking for updates: {ex.Message}");
            Debug.WriteLine($"Stack trace: {ex.StackTrace}");
            return (false, _currentVersion, null);
        }
    }

    public async Task<bool> DownloadAndInstallUpdateAsync(string downloadUrl)
    {
        var dialog = new ContentDialog
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

        if (string.IsNullOrWhiteSpace(downloadUrl))
        {
            Debug.WriteLine("Download URL is empty. Cannot download update.");
            return false;
        }

        try
        {
            if (!IsDotNetRuntimeInstalled())
            {
                Debug.WriteLine(".NET Runtime is not installed or version is too old.");
                return false;
            }

            var fileName = Path.GetFileName(new Uri(downloadUrl).AbsolutePath);
            if (string.IsNullOrWhiteSpace(fileName)) fileName = "MultiSavesBackupSetup.exe";
            var tempFile = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}_{fileName}");

            Debug.WriteLine($"Downloading update from {downloadUrl} to {tempFile}");

            _ = dialog.ShowAsync();

            var response = await _httpClient.GetAsync(downloadUrl);
            response.EnsureSuccessStatusCode();
            Debug.WriteLine($"Download response status: {response.StatusCode}");

            using (var fileStream = File.Create(tempFile))
            {
                await response.Content.CopyToAsync(fileStream);
            }

            Debug.WriteLine($"Download complete: {tempFile}");

            var startInfo = new ProcessStartInfo
            {
                FileName = tempFile,
                Arguments = "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /CLOSEAPPLICATIONS",
                UseShellExecute = true,
                Verb = "runas",
                CreateNoWindow = true
            };

            Debug.WriteLine($"Starting installer with silent parameters: {tempFile}");
            try
            {
                var process = Process.Start(startInfo);
                if (process == null)
                {
                    Debug.WriteLine("Failed to start the installer process");
                    dialog.Hide();
                    return false;
                }

                Debug.WriteLine("Installer started successfully");
                Debug.WriteLine("Exiting application to allow update to proceed.");
                Environment.Exit(0);
                return true;
            }
            catch (Win32Exception ex)
            {
                Debug.WriteLine($"Failed to start installer (possibly due to UAC): {ex.Message}");
                Debug.WriteLine($"Error code: {ex.NativeErrorCode}");
                Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                dialog.Hide();
                return false;
            }
        }
        catch (HttpRequestException ex)
        {
            Debug.WriteLine($"HTTP error downloading update: {ex.Message}");
            Debug.WriteLine($"Stack trace: {ex.StackTrace}");
            dialog.Hide();
            return false;
        }
        catch (IOException ex)
        {
            Debug.WriteLine($"IO error saving update: {ex.Message}");
            Debug.WriteLine($"Stack trace: {ex.StackTrace}");
            dialog.Hide();
            return false;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error downloading or installing update: {ex.Message}");
            Debug.WriteLine($"Stack trace: {ex.StackTrace}");
            dialog.Hide();
            return false;
        }
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