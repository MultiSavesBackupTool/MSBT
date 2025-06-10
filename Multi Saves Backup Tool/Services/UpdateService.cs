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

                if (release == null || string.IsNullOrEmpty(release.TagName))
                {
                    Debug.WriteLine("Failed to deserialize release or tag name is empty");
                    return (false, _currentVersion, null);
                }

                Debug.WriteLine($"Successfully deserialized release. Tag: {release.TagName}, Name: {release.Name}");

                var latestVersionString = release.TagName.TrimStart('v');
                Debug.WriteLine($"Current version: {_currentVersion}, Latest version: {latestVersionString}");

                if (!latestVersionString.Contains('.'))
                    latestVersionString = $"{latestVersionString}.0.0.0";

                if (!Version.TryParse(latestVersionString, out var latestVersionParsed) ||
                    !Version.TryParse(_currentVersion, out var currentVersionParsed))
                {
                    Debug.WriteLine(
                        $"Could not parse versions for comparison: '{latestVersionString}' and '{_currentVersion}'");
                    var hasUpdateFallback = string.Compare(latestVersionString, _currentVersion,
                        StringComparison.OrdinalIgnoreCase) > 0;
                    var downloadUrlFallback = GetDownloadUrlForCurrentPlatform(release.Assets);
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

    private string? GetDownloadUrlForCurrentPlatform(List<GitHubAsset>? assets)
    {
        if (assets == null || !assets.Any())
            return null;

        if (OperatingSystem.IsMacOS())
            return assets.FirstOrDefault(a => a.Name.EndsWith(".dmg", StringComparison.OrdinalIgnoreCase))
                ?.BrowserDownloadUrl;

        if (OperatingSystem.IsWindows())
            return assets.FirstOrDefault(a => a.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                ?.BrowserDownloadUrl;

        if (OperatingSystem.IsLinux())
            return assets.FirstOrDefault(a => a.Name.EndsWith(".AppImage", StringComparison.OrdinalIgnoreCase))
                ?.BrowserDownloadUrl;

        return assets.FirstOrDefault()?.BrowserDownloadUrl;
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

            if (OperatingSystem.IsMacOS()) return await InstallUpdateOnMacOS(tempFile);

            if (OperatingSystem.IsWindows()) return await InstallUpdateOnWindows(tempFile);

            if (OperatingSystem.IsLinux()) return await InstallUpdateOnLinux(tempFile);

            dialog.Hide();
            return false;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error downloading or installing update: {ex.Message}");
            dialog.Hide();
            return false;
        }
    }

    private async Task<bool> InstallUpdateOnMacOS(string filePath)
    {
        try
        {
            return await InstallDmgFile(filePath);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error installing update on macOS: {ex.Message}");
            return false;
        }
    }

    private async Task<bool> InstallDmgFile(string dmgPath)
    {
        try
        {
            var mountProcess = new ProcessStartInfo
            {
                FileName = "hdiutil",
                Arguments = $"attach \"{dmgPath}\" -nobrowse -quiet",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            Debug.WriteLine($"Mounting DMG: {dmgPath}");
            using var process = Process.Start(mountProcess);
            if (process == null)
            {
                Debug.WriteLine("Failed to start hdiutil process");
                return false;
            }

            await process.WaitForExitAsync();
            var output = await process.StandardOutput.ReadToEndAsync();
            var error = await process.StandardError.ReadToEndAsync();

            if (process.ExitCode != 0)
            {
                Debug.WriteLine($"Failed to mount DMG. Exit code: {process.ExitCode}, Error: {error}");
                return false;
            }

            var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            var volumePath = lines.LastOrDefault()?.Split('\t').LastOrDefault()?.Trim();

            if (string.IsNullOrEmpty(volumePath))
            {
                Debug.WriteLine("Could not determine mounted volume path");
                return false;
            }

            Debug.WriteLine($"DMG mounted at: {volumePath}");

            var appFiles = Directory.GetDirectories(volumePath, "*.app");
            if (!appFiles.Any())
            {
                Debug.WriteLine("No .app files found in mounted DMG");
                await UnmountDmg(volumePath);
                return false;
            }

            var appPath = appFiles.First();
            var appName = Path.GetFileName(appPath);
            var destinationPath = Path.Combine("/Applications", appName);

            Debug.WriteLine($"Copying {appPath} to {destinationPath}");

            var copyProcess = new ProcessStartInfo
            {
                FileName = "cp",
                Arguments = $"-R \"{appPath}\" \"/Applications/\"",
                UseShellExecute = false,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var copyProc = Process.Start(copyProcess);
            if (copyProc != null)
            {
                await copyProc.WaitForExitAsync();
                if (copyProc.ExitCode == 0)
                {
                    Debug.WriteLine("Application copied successfully");

                    await UnmountDmg(volumePath);

                    var openProcess = new ProcessStartInfo
                    {
                        FileName = "open",
                        Arguments = $"\"{destinationPath}\"",
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };

                    Process.Start(openProcess);

                    Environment.Exit(0);
                    return true;
                }

                var copyError = await copyProc.StandardError.ReadToEndAsync();
                Debug.WriteLine($"Failed to copy application. Error: {copyError}");
            }

            await UnmountDmg(volumePath);
            return false;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error installing DMG file: {ex.Message}");
            return false;
        }
    }

    private async Task UnmountDmg(string volumePath)
    {
        try
        {
            var unmountProcess = new ProcessStartInfo
            {
                FileName = "hdiutil",
                Arguments = $"detach \"{volumePath}\" -quiet",
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(unmountProcess);
            if (process != null)
            {
                await process.WaitForExitAsync();
                Debug.WriteLine($"DMG unmounted: {volumePath}");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error unmounting DMG: {ex.Message}");
        }
    }

    private Task<bool> InstallUpdateOnWindows(string filePath)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = filePath,
            Arguments = "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /CLOSEAPPLICATIONS",
            UseShellExecute = true,
            Verb = "runas",
            CreateNoWindow = true
        };

        try
        {
            var process = Process.Start(startInfo);
            if (process != null)
            {
                Environment.Exit(0);
                return Task.FromResult(true);
            }
        }
        catch (Win32Exception ex)
        {
            Debug.WriteLine($"Failed to start installer: {ex.Message}");
        }

        return Task.FromResult(false);
    }

    private async Task<bool> InstallUpdateOnLinux(string filePath)
    {
        var extension = Path.GetExtension(filePath).ToLowerInvariant();

        try
        {
            if (extension == ".appimage")
            {
                var chmodProcess = new ProcessStartInfo
                {
                    FileName = "chmod",
                    Arguments = $"+x \"{filePath}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(chmodProcess);
                if (process != null)
                {
                    await process.WaitForExitAsync();
                    if (process.ExitCode == 0)
                    {
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = filePath,
                            UseShellExecute = false,
                            CreateNoWindow = true
                        });
                        Environment.Exit(0);
                        return true;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error installing update on Linux: {ex.Message}");
        }

        return false;
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