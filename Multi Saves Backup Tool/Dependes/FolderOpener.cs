using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace Multi_Saves_Backup_Tool.Dependes;

public static class FolderOpener
{
    public static void OpenFolder(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Path cannot be empty", nameof(path));

        if (!Directory.Exists(path))
            throw new DirectoryNotFoundException($"Folder not found: {path}");

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer",
                Arguments = $"\"{path}\"",
                UseShellExecute = true
            });
        else
            throw new PlatformNotSupportedException("Unsupported operating system");
    }
}