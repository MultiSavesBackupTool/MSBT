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
    
    public MonitoringViewModel MonitoringViewModel { get; }
    public GamesViewModel GamesViewModel { get; }
    public ArchivesViewModel ArchivesViewModel { get; }
    
    public MainWindowViewModel()
    {
        MonitoringViewModel = new MonitoringViewModel();
        GamesViewModel = new GamesViewModel();
        ArchivesViewModel = new ArchivesViewModel();
        
        this.WhenAnyValue(x => x.SelectedMenuItem)
            .Where(item => item != null)
            .Subscribe(OnNavigationChanged);
        
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
            _ => MonitoringViewModel
        };
        
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
        Debug.WriteLine("Переключение на Мониторинг");
    }
    
    private void OnGamesSelected()
    {
        Debug.WriteLine("Переключение на Игры");
    }
    
    private void OnArchivesSelected()
    {
        Debug.WriteLine("Переключение на Архивы");
    }
}