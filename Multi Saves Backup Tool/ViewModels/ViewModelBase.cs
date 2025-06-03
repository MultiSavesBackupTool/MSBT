using ReactiveUI;

namespace Multi_Saves_Backup_Tool.ViewModels;

public abstract class ViewModelBase : ReactiveObject
{
    public abstract string Title { get; }
}