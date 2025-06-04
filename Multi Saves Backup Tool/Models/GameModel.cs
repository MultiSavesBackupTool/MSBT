using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Multi_Saves_Backup_Tool.Models;

public class GameModel : INotifyPropertyChanged
{
    private string _gameName = string.Empty;
    private string _gameExe = string.Empty;
    private string? _gameExeAlt;
    private string _savePath = string.Empty;
    private string? _modPath;
    private string? _addPath;
    private int _daysForKeep;
    private int _setOldFilesStatus;
    private bool _isEnabled = true;

    public string GameName
    {
        get => _gameName;
        set => SetField(ref _gameName, value);
    }

    public string GameExe
    {
        get => _gameExe;
        set => SetField(ref _gameExe, value);
    }

    public string? GameExeAlt
    {
        get => _gameExeAlt;
        set => SetField(ref _gameExeAlt, value);
    }

    public string SavePath
    {
        get => _savePath;
        set => SetField(ref _savePath, value);
    }

    public string? ModPath
    {
        get => _modPath;
        set => SetField(ref _modPath, value);
    }

    public string? AddPath
    {
        get => _addPath;
        set => SetField(ref _addPath, value);
    }

    public int DaysForKeep
    {
        get => _daysForKeep;
        set => SetField(ref _daysForKeep, value);
    }

    public int SetOldFilesStatus
    {
        get => _setOldFilesStatus;
        set => SetField(ref _setOldFilesStatus, value);
    }

    public bool IsEnabled
    {
        get => _isEnabled;
        set => SetField(ref _isEnabled, value);
    }
    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}
