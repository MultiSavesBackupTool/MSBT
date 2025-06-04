using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Multi_Saves_Backup_Tool.ViewModels;

namespace Multi_Saves_Backup_Tool.Views;

public partial class AddGameOverlay : UserControl
{
    public AddGameOverlay()
    {
        InitializeComponent();
    }

    private AddGameOverlayViewModel ViewModel => DataContext as AddGameOverlayViewModel
                                                 ?? throw new InvalidOperationException(
                                                     "DataContext is not AddGameOverlayViewModel");

    public void Initialize(GamesViewModel gamesViewModel)
    {
        var viewModel = new AddGameOverlayViewModel(gamesViewModel);
        viewModel.CloseRequested += (_, _) => IsVisible = false;
        DataContext = viewModel;
    }

    public void Show()
    {
        IsVisible = true;
        ViewModel.ClearForm();
    }

    private void Close(object? sender, RoutedEventArgs e)
    {
        IsVisible = false;
    }
}