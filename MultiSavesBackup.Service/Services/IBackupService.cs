using Multi_Saves_Backup_Tool.Models;

namespace MultiSavesBackup.Service.Services;

public interface IBackupService
{
    Task CreateBackupAsync(GameModel game);
    Task CleanupOldBackupsAsync(GameModel game);
    Task<bool> VerifyBackupPathsAsync(GameModel game);
}
