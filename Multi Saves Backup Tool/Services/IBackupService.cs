using System;
using System.Threading.Tasks;
using Multi_Saves_Backup_Tool.Models;

namespace Multi_Saves_Backup_Tool.Services;

public interface IBackupService : IDisposable
{
    Task ProcessSpecialBackup(GameModel game);
    Task CreateBackupAsync(GameModel game, bool isPermanent);
    Task RestoreLatestBackupAsync(GameModel game);
    void CleanupOldBackups(GameModel game);
    bool VerifyBackupPaths(GameModel game);
    int GetBackupCount(GameModel game);
}