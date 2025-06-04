using Avalonia.Controls;
using Avalonia.Interactivity;
using Multi_Saves_Backup_Tool.ViewModels;

namespace Multi_Saves_Backup_Tool.Views;

public partial class GamesView : UserControl
{
    public GamesView()
    {
        InitializeComponent();
        DataContext = new GamesViewModel();
    }

    private void AddGameButton_Click(object sender, RoutedEventArgs e)
    {
        if (TopLevel.GetTopLevel(this) is MainWindow mainWindow && mainWindow.AddGameOverlay != null)
        {
            mainWindow.AddGameOverlay.Show();
        }
    }
}
