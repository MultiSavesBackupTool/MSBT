using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace Multi_Saves_Backup_Tool.Dependes;

public static class FolderOpener
{
    public static void OpenFolder(string? path)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                Debug.WriteLine("Path cannot be empty");
                return;
            }

            if (!Directory.Exists(path))
            {
                Debug.WriteLine($"Folder not found: {path}");
                return;
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                Process.Start(new ProcessStartInfo
                {
                    FileName = "explorer",
                    Arguments = $"\"{path}\"",
                    UseShellExecute = true
                });
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                Process.Start(new ProcessStartInfo
                {
                    FileName = "xdg-open",
                    Arguments = $"\"{path}\"",
                    UseShellExecute = true
                });
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                Process.Start(new ProcessStartInfo
                {
                    FileName = "open",
                    Arguments = $"\"{path}\"",
                    UseShellExecute = true
                });
            else
                Debug.WriteLine("Unsupported operating system");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error opening folder: {ex.Message}");
        }
    }
}