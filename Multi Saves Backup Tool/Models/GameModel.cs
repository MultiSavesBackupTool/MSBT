using CommunityToolkit.Mvvm.ComponentModel;

namespace Multi_Saves_Backup_Tool.Models;
public class GameModel
{
    public string GameExe { get; set; } = string.Empty;
    public string GameExeAlt { get; set; } = string.Empty;
    public string GameName { get; set; } = string.Empty;
    public string SavePath { get; set; } = string.Empty;
    public int DaysForKeep { get; set; }
    public int SetOldFilesStatus { get; set; }
    public string ModPath { get; set; } = string.Empty;
    public string AddPath { get; set; } = string.Empty;
    public char AddOrUpdate { get; set; } = 'a';
    public bool UseDateTime { get; set; } = true;
}
