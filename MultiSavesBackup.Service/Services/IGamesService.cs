using Multi_Saves_Backup_Tool.Models;

namespace MultiSavesBackup.Service.Services;

public interface IGamesService
{
    Task<IReadOnlyList<GameModel>> LoadGamesAsync();
    Task<GameModel?> GetGameByNameAsync(string gameName);
    bool IsGameRunning(GameModel game);
}
