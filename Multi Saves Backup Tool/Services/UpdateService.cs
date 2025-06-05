using System;
using System.Net.Http;
using System.Threading.Tasks;
using System.Text.Json;
using System.IO;
using System.Diagnostics;
using System.Reflection;
using System.Collections.Generic;
using System.Linq;

namespace Multi_Saves_Backup_Tool.Services
{
    public class UpdateService
    {
        private const string GITHUB_API_URL = "https://api.github.com/repos/{owner}/{repo}/releases/latest";
        private const string OWNER = "TheNightlyGod";
        private const string REPO = "MSBT";
        
        private readonly HttpClient _httpClient;
        private readonly string _currentVersion;

        public UpdateService()
        {
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "MultiSavesBackupToolUpdateChecker");
            _currentVersion = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0.0";
        }

        public async Task<(bool hasUpdate, string latestVersion, string? downloadUrl)> CheckForUpdatesAsync()
        {
            try
            {
                var apiUrl = GITHUB_API_URL.Replace("{owner}", OWNER).Replace("{repo}", REPO);
                var response = await _httpClient.GetStringAsync(apiUrl);
                
                var release = JsonSerializer.Deserialize<GitHubRelease>(response);
                
                if (release == null || string.IsNullOrEmpty(release.TagName))
                {
                    Debug.WriteLine("Failed to deserialize release or tag name is empty.");
                    return (false, _currentVersion, null);
                }

                var latestVersionString = release.TagName.TrimStart('v');
                
                if (!Version.TryParse(latestVersionString, out var latestVersionParsed) ||
                    !Version.TryParse(_currentVersion, out var currentVersionParsed))
                {
                    Debug.WriteLine($"Could not parse versions for comparison: '{latestVersionString}' and '{_currentVersion}'");
                    bool hasUpdateFallback = string.Compare(latestVersionString, _currentVersion, StringComparison.OrdinalIgnoreCase) > 0;
                     var downloadUrlFallback = release.Assets?.FirstOrDefault(a => a.Name.EndsWith(".exe"))?.BrowserDownloadUrl;
                    return (hasUpdateFallback, latestVersionString, downloadUrlFallback);
                }

                bool hasUpdate = latestVersionParsed.CompareTo(currentVersionParsed) > 0;
                string? downloadUrl = null;
                if (hasUpdate)
                {
                    downloadUrl = release.Assets?.FirstOrDefault(a => a.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))?.BrowserDownloadUrl 
                                  ?? release.Assets?.FirstOrDefault()?.BrowserDownloadUrl;
                }
                
                if (hasUpdate && string.IsNullOrEmpty(downloadUrl))
                {
                    Debug.WriteLine($"Update detected (v{latestVersionString}) but no suitable download URL found in assets.");
                }
                
                return (hasUpdate, latestVersionString, downloadUrl);
            }
            catch (HttpRequestException ex)
            {
                Debug.WriteLine($"HTTP request error checking for updates: {ex.Message}");
                return (false, _currentVersion, null);
            }
            catch (JsonException ex)
            {
                 Debug.WriteLine($"JSON deserialization error checking for updates: {ex.Message}");
                return (false, _currentVersion, null);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Generic error checking for updates: {ex.Message}");
                return (false, _currentVersion, null);
            }
        }

        public async Task<bool> DownloadAndInstallUpdateAsync(string downloadUrl)
        {
            if (string.IsNullOrWhiteSpace(downloadUrl))
            {
                Debug.WriteLine("Download URL is empty. Cannot download update.");
                return false;
            }

            try
            {
                var fileName = Path.GetFileName(new Uri(downloadUrl).AbsolutePath);
                if (string.IsNullOrWhiteSpace(fileName))
                {
                    fileName = "MultiSavesBackupSetup.exe";
                }
                var tempFile = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}_{fileName}");
                
                Debug.WriteLine($"Downloading update from {downloadUrl} to {tempFile}");
                var response = await _httpClient.GetAsync(downloadUrl);
                
                using (var fileStream = File.Create(tempFile))
                {
                    await response.Content.CopyToAsync(fileStream);
                }
                Debug.WriteLine($"Download complete: {tempFile}");

                var startInfo = new ProcessStartInfo
                {
                    FileName = tempFile,
                    UseShellExecute = true
                };
                
                Debug.WriteLine($"Starting installer: {tempFile}");
                Process.Start(startInfo);

                 Debug.WriteLine("Exiting application to allow update to proceed.");
                Environment.Exit(0); 
                return true;
            }
            catch (HttpRequestException ex)
            {
                 Debug.WriteLine($"HTTP error downloading update: {ex.Message}");
                return false;
            }
            catch (IOException ex)
            {
                Debug.WriteLine($"IO error saving update: {ex.Message}");
                return false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error downloading or installing update: {ex.Message}");
                return false;
            }
        }
    }

    public class GitHubRelease
    {
        public string TagName { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty; 
        public string Body { get; set; } = string.Empty; 
        public List<GitHubAsset>? Assets { get; set; }
    }

    public class GitHubAsset
    {
        public string Name { get; set; } = string.Empty;
        public string BrowserDownloadUrl { get; set; } = string.Empty;
    }
} 