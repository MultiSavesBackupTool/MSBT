using Avalonia.Controls;
using Avalonia.Interactivity;
using Multi_Saves_Backup_Tool.Models;
using Multi_Saves_Backup_Tool.ViewModels;

namespace Multi_Saves_Backup_Tool.Views;

public partial class GamesView : UserControl
{
    private readonly GamesViewModel _viewModel;

    public GamesView()
    {
        InitializeComponent();
        _viewModel = new GamesViewModel();
        DataContext = _viewModel;

        _viewModel.EditGameRequested += OnEditGameRequested;

        Loaded += GamesView_Loaded;
    }

    private void GamesView_Loaded(object? sender, RoutedEventArgs e)
    {
        foreach (var game in _viewModel.Games)
            _viewModel.UpdateBackupCount(game);
    }

    private void OnEditGameRequested(object? sender, GameModel game)
    {
        if (TopLevel.GetTopLevel(this) is MainWindow mainWindow && mainWindow.AddGameOverlay != null)
        {
            var overlay = mainWindow.AddGameOverlay;
            overlay.ShowForEdit(_viewModel, game);
        }
    }

    private void AddGameButton_Click(object sender, RoutedEventArgs e)
    {
        if (TopLevel.GetTopLevel(this) is MainWindow mainWindow && mainWindow.AddGameOverlay != null)
        {
            var overlay = mainWindow.AddGameOverlay;
            overlay.ShowForAdd(_viewModel);
        }
    }
}