using System.Text.Json.Serialization;

namespace Multi_Saves_Backup_Tool.Models;

public class WhitelistEntry
{
    public WhitelistEntry()
    {
    }

    public WhitelistEntry(string gameName, string gameExe, string? gameExeAlt, string savePath, string? modPath,
        string? addPath, bool specialBackupMark)
    {
        GameName = gameName;
        GameExe = gameExe;
        GameExeAlt = gameExeAlt;
        SavePath = savePath;
        ModPath = modPath;
        AddPath = addPath;
        SpecialBackupMark = specialBackupMark;
    }

    [JsonPropertyName("gameName")] public string GameName { get; set; } = string.Empty;

    [JsonPropertyName("gameExe")] public string GameExe { get; set; } = string.Empty;

    [JsonPropertyName("gameExeAlt")] public string? GameExeAlt { get; set; }

    [JsonPropertyName("savePath")] public string SavePath { get; set; } = string.Empty;

    [JsonPropertyName("modPath")] public string? ModPath { get; set; }

    [JsonPropertyName("addPath")] public string? AddPath { get; set; }

    [JsonPropertyName("specialBackupMark")]
    public bool SpecialBackupMark { get; set; }

    public GameModel ToGameModel()
    {
        return new GameModel
        {
            GameName = GameName,
            GameExe = GameExe,
            GameExeAlt = GameExeAlt,
            SavePath = SavePath,
            ModPath = ModPath,
            AddPath = AddPath,
            SpecialBackupMark = SpecialBackupMark,
            IsEnabled = false
        };
    }

    public static WhitelistEntry FromGameModel(GameModel game)
    {
        return new WhitelistEntry(
            game.GameName ?? string.Empty,
            game.GameExe,
            game.GameExeAlt,
            game.SavePath ?? string.Empty,
            game.ModPath,
            game.AddPath,
            game.SpecialBackupMark
        );
    }
}