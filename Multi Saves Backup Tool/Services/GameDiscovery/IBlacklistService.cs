using System.Collections.Generic;
using System.Threading.Tasks;

namespace Multi_Saves_Backup_Tool.Services.GameDiscovery;

public interface IBlacklistService
{
    Task<IEnumerable<string>> GetBlacklistAsync();
    Task AddToBlacklistAsync(string gameName);
    Task RemoveFromBlacklistAsync(string gameName);
    Task SyncWithServerAsync();
    Task ContributeToServerAsync(string gameName);
    bool IsBlacklisted(string gameName);
    int GetBlacklistCount();
}