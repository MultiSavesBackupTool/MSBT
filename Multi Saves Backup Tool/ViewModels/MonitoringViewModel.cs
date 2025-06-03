using ReactiveUI;

namespace Multi_Saves_Backup_Tool.ViewModels;

public class MonitoringViewModel : ViewModelBase
{
    public override string Title => "Мониторинг";
    
    private string _status = "Готов";
    public string Status
    {
        get => _status;
        set => this.RaiseAndSetIfChanged(ref _status, value);
    }
    
    public void RefreshData()
    {
        Status = "Обновление данных мониторинга...";
        // Логика обновления
    }
}