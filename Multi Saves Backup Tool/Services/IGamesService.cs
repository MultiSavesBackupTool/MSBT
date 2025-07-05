using System.Collections.Generic;
using System.Threading.Tasks;
using Multi_Saves_Backup_Tool.Models;

namespace Multi_Saves_Backup_Tool.Services;

public interface IGamesService
{
    Task<IReadOnlyList<GameModel?>> LoadGamesAsync();
    Task<GameModel?> GetGameByNameAsync(string? gameName);
    bool IsGameRunning(GameModel? game);
    Task SaveGamesAsync(IEnumerable<GameModel?>? games);
}