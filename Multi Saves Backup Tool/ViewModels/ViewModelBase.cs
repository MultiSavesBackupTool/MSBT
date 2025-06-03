using ReactiveUI;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Multi_Saves_Backup_Tool.ViewModels;

public abstract partial class ViewModelBase : ReactiveObject
{
    public abstract string Title { get; }
}

