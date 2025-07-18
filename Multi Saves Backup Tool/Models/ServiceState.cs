using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Multi_Saves_Backup_Tool.Models;

public class ServiceState
{
    public DateTime LastUpdateTime { get; set; }
    public Dictionary<string, GameState> GamesState { get; set; } = new();
    public string ServiceStatus { get; set; } = "Running";

    public void SaveToFile(string path, ServiceState state)
    {
        var json = JsonSerializer.Serialize(state);
        File.WriteAllText(path, json);
    }

    public static ServiceState LoadFromFile(string path)
    {
        try
        {
            if (!File.Exists(path))
                return new ServiceState();

            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<ServiceState>(json) ?? new ServiceState();
        }
        catch (Exception)
        {
            return new ServiceState();
        }
    }
}

public class GameState
{
    public string? GameName { get; set; } = "";
    public DateTime? LastBackupTime { get; set; }
    public string Status { get; set; } = "Waiting";
    public DateTime? NextBackupScheduled { get; set; }
    public string LastError { get; set; } = "";
    public bool IsRun { get; set; }
}