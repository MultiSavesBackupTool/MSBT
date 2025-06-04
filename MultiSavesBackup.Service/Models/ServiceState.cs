using System.Text.Json;

namespace MultiSavesBackup.Service.Models;

public class ServiceState
{
    public DateTime LastUpdateTime { get; set; }
    public Dictionary<string, GameState> GamesState { get; set; } = new();
    public string ServiceStatus { get; set; } = "Running";
    
    public void SaveToFile(string path)
    {
        var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(path, json);
    }
    
    public static ServiceState LoadFromFile(string path)
    {
        if (!File.Exists(path))
            return new ServiceState();
        
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<ServiceState>(json) ?? new ServiceState();
    }
}

public class GameState
{
    public string GameName { get; set; } = "";
    public DateTime? LastBackupTime { get; set; }
    public string Status { get; set; } = "Waiting";
    public DateTime? NextBackupScheduled { get; set; }
    public string LastError { get; set; } = "";
}
