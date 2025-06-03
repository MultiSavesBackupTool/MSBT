using CommunityToolkit.Mvvm.ComponentModel;

namespace Multi_Saves_Backup_Tool.ViewModels;

public partial class MonitoringViewModel : ViewModelBase
{
    public override string Title => "Monitoring";

    [ObservableProperty]
    private string _status = "Мониторинг запущен";
}
