using Avalonia.Controls;
using Avalonia.Interactivity;
using Multi_Saves_Backup_Tool.Models;
using Multi_Saves_Backup_Tool.ViewModels;

namespace Multi_Saves_Backup_Tool.Views;

public partial class GamesView : UserControl
{
    public GamesView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not GamesViewModel viewModel) return;
        
        viewModel.EditGameRequested += OnEditGameRequested;
        
        foreach (var game in viewModel.Games)
            viewModel.UpdateBackupCount(game);
    }

    private void OnEditGameRequested(object? sender, GameModel game)
    {
        if (DataContext is not GamesViewModel viewModel) return;
        if (TopLevel.GetTopLevel(this) is MainWindow mainWindow && mainWindow.AddGameOverlay != null)
        {
            var overlay = mainWindow.AddGameOverlay;
            overlay.ShowForEdit(viewModel, game);
        }
    }

    private void AddGameButton_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not GamesViewModel viewModel) return;
        if (TopLevel.GetTopLevel(this) is MainWindow mainWindow && mainWindow.AddGameOverlay != null)
        {
            var overlay = mainWindow.AddGameOverlay;
            overlay.ShowForAdd(viewModel);
        }
    }
}