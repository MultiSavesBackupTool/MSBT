using System.Collections.Generic;
using System.Threading.Tasks;
using Multi_Saves_Backup_Tool.Models;

namespace Multi_Saves_Backup_Tool.Services.GameDiscovery;

public interface IWhitelistService
{
    Task<IEnumerable<WhitelistEntry>> GetWhitelistAsync();
    Task AddToWhitelistAsync(WhitelistEntry entry);
    Task RemoveFromWhitelistAsync(string gameName);
    Task SyncWithServerAsync();
    Task ContributeToServerAsync(WhitelistEntry entry);
    bool IsWhitelisted(string gameName);
    WhitelistEntry? GetWhitelistEntry(string gameName);
    int GetWhitelistCount();
}