namespace Multi_Saves_Backup_Tool.Models;

public class GameModel
{
    public string GameName { get; set; } = string.Empty;
    public string GameExe { get; set; } = string.Empty;
    public string? GameExeAlt { get; set; }
    public string SavePath { get; set; } = string.Empty;
    public string? ModPath { get; set; }
    public string? AddPath { get; set; }
    public int DaysForKeep { get; set; }
    public int SetOldFilesStatus { get; set; }
    public bool IsEnabled { get; set; } = true;
}
