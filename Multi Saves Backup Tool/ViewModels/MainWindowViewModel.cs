using System;
using System.Diagnostics;
using System.Reactive.Linq;
using FluentAvalonia.UI.Controls;
using ReactiveUI;

namespace Multi_Saves_Backup_Tool.ViewModels;

public class MainWindowViewModel : ReactiveObject
{
    private NavigationViewItem? _selectedMenuItem;
    private ViewModelBase? _currentViewModel;
    
    public NavigationViewItem? SelectedMenuItem
    {
        get => _selectedMenuItem;
        set => this.RaiseAndSetIfChanged(ref _selectedMenuItem, value);
    }
    
    public ViewModelBase? CurrentViewModel
    {
        get => _currentViewModel;
        set => this.RaiseAndSetIfChanged(ref _currentViewModel, value);
    }
    
    // ViewModels для каждой секции
    public MonitoringViewModel MonitoringViewModel { get; }
    public GamesViewModel GamesViewModel { get; }
    public ArchivesViewModel ArchivesViewModel { get; }
    
    public MainWindowViewModel()
    {
        // Инициализация ViewModels
        MonitoringViewModel = new MonitoringViewModel();
        GamesViewModel = new GamesViewModel();
        ArchivesViewModel = new ArchivesViewModel();
        
        // Реакция на изменение выбранного пункта меню
        this.WhenAnyValue(x => x.SelectedMenuItem)
            .Where(item => item != null)
            .Subscribe(OnNavigationChanged);
        
        // Устанавливаем начальное значение
        CurrentViewModel = MonitoringViewModel;
    }
    
    private void OnNavigationChanged(NavigationViewItem? selectedItem)
    {
        var tag = selectedItem?.Tag?.ToString();
        
        CurrentViewModel = tag switch
        {
            "monitoring" => MonitoringViewModel,
            "games" => GamesViewModel,
            "archives" => ArchivesViewModel,
            _ => MonitoringViewModel // по умолчанию
        };
        
        // Дополнительная логика при переключении
        switch (tag)
        {
            case "monitoring":
                OnMonitoringSelected();
                break;
            case "games":
                OnGamesSelected();
                break;
            case "archives":
                OnArchivesSelected();
                break;
        }
    }
    
    private void OnMonitoringSelected()
    {
        // Логика при выборе мониторинга
        MonitoringViewModel.RefreshData();
        Debug.WriteLine("Переключение на Мониторинг");
    }
    
    private void OnGamesSelected()
    {
        
    }
    
    private void OnArchivesSelected()
    {
        // Логика при выборе архивов
        ArchivesViewModel.LoadArchives();
        Debug.WriteLine("Переключение на Архивы");
    }
}
