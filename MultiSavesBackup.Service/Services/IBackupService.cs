using Multi_Saves_Backup_Tool.Models;

namespace MultiSavesBackup.Service.Services;

public interface IBackupService
{
    Task CreateBackupAsync(GameModel game);
    void CleanupOldBackups(GameModel game);
    bool VerifyBackupPaths(GameModel game);
}
