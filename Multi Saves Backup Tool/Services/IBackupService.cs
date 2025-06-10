using System.Threading.Tasks;
using Multi_Saves_Backup_Tool.Models;

namespace Multi_Saves_Backup_Tool.Services;

public interface IBackupService
{
    Task ProcessSpecialBackup(GameModel game);
    Task CreateBackupAsync(GameModel game);
    void CleanupOldBackups(GameModel game);
    bool VerifyBackupPaths(GameModel game);
}