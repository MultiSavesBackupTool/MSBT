using CommunityToolkit.Mvvm.ComponentModel;

namespace Multi_Saves_Backup_Tool.ViewModels;

public partial class ArchivesViewModel : ViewModelBase
{
    public override string Title => "Archives";

    [ObservableProperty]
    private int _archiveCount = 0;
}
