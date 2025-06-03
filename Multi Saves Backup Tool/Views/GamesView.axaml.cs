using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Multi_Saves_Backup_Tool.Views;

public partial class GamesView : UserControl
{
    public GamesView()
    {
        InitializeComponent();
    }

    private void AddGameButton_Click(object sender, RoutedEventArgs e)
    {
        if (TopLevel.GetTopLevel(this) is MainWindow mainWindow && mainWindow.AddGameOverlay != null)
        {
            mainWindow.AddGameOverlay.Show();
        }
    }
}
