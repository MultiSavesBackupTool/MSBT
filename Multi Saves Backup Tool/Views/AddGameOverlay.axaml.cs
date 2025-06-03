using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using System.Threading.Tasks;

namespace Multi_Saves_Backup_Tool.Views;

public partial class AddGameOverlay : UserControl
{
    public AddGameOverlay()
    {
        InitializeComponent();
    }

    public void Show()
    {
        IsVisible = true;
        ClearForm();
    }

    private void Close(object? sender, RoutedEventArgs e)
    {
        IsVisible = false;
    }

    private void ClearForm()
    {
        if (GameNameTextBox != null) GameNameTextBox.Text = string.Empty;
        if (GameExeTextBox != null) GameExeTextBox.Text = string.Empty;
        if (GameExeAltTextBox != null) GameExeAltTextBox.Text = string.Empty;
        if (SaveLocationTextBox != null) SaveLocationTextBox.Text = string.Empty;
        if (ModPathTextBox != null) ModPathTextBox.Text = string.Empty;
        if (AddPathTextBox != null) AddPathTextBox.Text = string.Empty;
        if (DaysForKeepNumeric != null) DaysForKeepNumeric.Value = 0;
        if (OldFilesStatusComboBox != null) OldFilesStatusComboBox.SelectedIndex = 0;
        if (IncludeTimestampCheck != null) IncludeTimestampCheck.IsChecked = true;
        if (BackupModeComboBox != null) BackupModeComboBox.SelectedIndex = 0;
    }

    private async void BrowseSaveLocation(object? sender, RoutedEventArgs e)
    {
        var folderPath = await BrowseFolder();
        if (SaveLocationTextBox != null && !string.IsNullOrEmpty(folderPath))
            SaveLocationTextBox.Text = folderPath;
    }

    private async Task<string> BrowseFolder()
    {
        var storageProvider = TopLevel.GetTopLevel(this)?.StorageProvider;
        if (storageProvider != null)
        {
            var folder = await storageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = "Select Folder",
                AllowMultiple = false
            });

            return folder.Count > 0 ? folder[0].Path.LocalPath : string.Empty;
        }
        return string.Empty;
    }

    private async void BrowseModPath(object? sender, RoutedEventArgs e)
    {
        var folderPath = await BrowseFolder();
        if (ModPathTextBox != null && !string.IsNullOrEmpty(folderPath))
            ModPathTextBox.Text = folderPath;
    }

    private async void BrowseAddPath(object? sender, RoutedEventArgs e)
    {
        var folderPath = await BrowseFolder();
        if (AddPathTextBox != null && !string.IsNullOrEmpty(folderPath))
            AddPathTextBox.Text = folderPath;
    }

    private void Add(object? sender, RoutedEventArgs e)
    {
        // TODO: Implement add logic
        Close(null, e);
    }
}
