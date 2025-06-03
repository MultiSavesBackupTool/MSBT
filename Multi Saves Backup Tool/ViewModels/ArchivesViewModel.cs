using ReactiveUI;

namespace Multi_Saves_Backup_Tool.ViewModels;

public class ArchivesViewModel : ViewModelBase
{
    public override string Title => "Архивы";
    
    private int _archiveCount;
    public int ArchiveCount
    {
        get => _archiveCount;
        set => this.RaiseAndSetIfChanged(ref _archiveCount, value);
    }
    
    public void LoadArchives()
    {
        ArchiveCount = 42; // Пример
        // Логика загрузки архивов
    }
}