using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using FluentAvalonia.UI.Controls;

namespace Multi_Saves_Backup_Tool.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    [ObservableProperty]
    private ViewModelBase? _currentViewModel;

    [ObservableProperty]
    private NavigationViewItem? _selectedMenuItem;

    public MonitoringViewModel MonitoringViewModel { get; }
    public GamesViewModel GamesViewModel { get; }
    public SettingsViewModel SettingsViewModel { get; }

    public MainWindowViewModel()
    {
        var window = new Window();
        MonitoringViewModel = new MonitoringViewModel();
        GamesViewModel = new GamesViewModel();
        SettingsViewModel = new SettingsViewModel(window.StorageProvider);
        CurrentViewModel = MonitoringViewModel;
    }

    public MainWindowViewModel(Window mainWindow)
    {
        MonitoringViewModel = new MonitoringViewModel();
        GamesViewModel = new GamesViewModel();
        SettingsViewModel = new SettingsViewModel(mainWindow.StorageProvider);
        CurrentViewModel = MonitoringViewModel;
    }
    
    partial void OnSelectedMenuItemChanged(NavigationViewItem? oldValue, NavigationViewItem? newValue)
    {
        if (newValue == null) return;

        CurrentViewModel = (newValue.Tag as string) switch
        {
            "monitoring" => MonitoringViewModel,
            "games" => GamesViewModel,
            "settings" => SettingsViewModel,
            _ => CurrentViewModel
        };
    }
}


