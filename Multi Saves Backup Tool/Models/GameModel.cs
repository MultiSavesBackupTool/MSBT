using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace Multi_Saves_Backup_Tool.Models;

public class GameModel : INotifyPropertyChanged
{
    private string? _addPath;
    private int _backupCount;
    private int _backupInterval = 60;
    private int _daysForKeep;
    private string _gameExe = string.Empty;
    private string? _gameExeAlt;
    private string? _gameName = string.Empty;
    private bool _isEnabled = true;
    private bool _isHidden;
    private bool _isSelected;
    private string? _modPath;
    private string? _platform;
    private string? _savePath = string.Empty;
    private int _setOldFilesStatus;
    private bool _specialBackupMark;

    [JsonIgnore] public bool ShowHiddenGames { get; set; }

    public string? GameName
    {
        get => _gameName;
        set
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("Game name cannot be null or whitespace", nameof(value));
            SetField(ref _gameName, value);
        }
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

    public string? SavePath
    {
        get => _savePath;
        set
        {
            if (!string.IsNullOrWhiteSpace(value) && !Path.IsPathRooted(value)) value = Path.GetFullPath(value);
            SetField(ref _savePath, value);
        }
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
        set
        {
            if (value < 0)
                throw new ArgumentException("Days for keep cannot be negative", nameof(value));
            SetField(ref _daysForKeep, value);
        }
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

    public int BackupInterval
    {
        get => _backupInterval;
        set
        {
            if (value < 1)
                throw new ArgumentException("Backup interval must be at least 1 minute", nameof(value));
            if (value > 1440)
                throw new ArgumentException("Backup interval cannot exceed 24 hours (1440 minutes)", nameof(value));
            SetField(ref _backupInterval, value);
        }
    }

    public int BackupCount
    {
        get => _backupCount;
        set
        {
            if (value < 0)
                throw new ArgumentException("Backup count cannot be negative", nameof(value));
            SetField(ref _backupCount, value);
        }
    }

    public bool SpecialBackupMark
    {
        get => _specialBackupMark;
        set => SetField(ref _specialBackupMark, value);
    }

    [JsonIgnore]
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected != value)
            {
                _isSelected = value;
                OnPropertyChanged();
            }
        }
    }

    public string? Platform
    {
        get => _platform;
        set => SetField(ref _platform, value);
    }

    public bool IsHidden
    {
        get => _isHidden;
        set => SetField(ref _isHidden, value);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        OnPropertyChanged(propertyName);
    }

    private bool IsValid()
    {
        return !string.IsNullOrWhiteSpace(_gameName) && !string.IsNullOrWhiteSpace(_gameExe);
    }

    public bool IsValidForBackup()
    {
        return IsValid() &&
               !string.IsNullOrWhiteSpace(_savePath) &&
               Directory.Exists(_savePath);
    }

    public bool HasValidPaths()
    {
        var hasValidPath = (!string.IsNullOrWhiteSpace(_savePath) && Directory.Exists(_savePath)) ||
                           (!string.IsNullOrWhiteSpace(_modPath) && Directory.Exists(_modPath)) ||
                           (!string.IsNullOrWhiteSpace(_addPath) && Directory.Exists(_addPath));

        return hasValidPath;
    }

    public GameModel Clone()
    {
        return new GameModel
        {
            GameName = GameName,
            GameExe = GameExe,
            GameExeAlt = GameExeAlt,
            SavePath = SavePath,
            ModPath = ModPath,
            AddPath = AddPath,
            DaysForKeep = DaysForKeep,
            SetOldFilesStatus = SetOldFilesStatus,
            IsEnabled = IsEnabled,
            BackupInterval = BackupInterval,
            BackupCount = BackupCount,
            SpecialBackupMark = SpecialBackupMark,
            IsHidden = IsHidden,
            Platform = Platform
        };
    }

    public override string ToString()
    {
        return $"{GameName} ({GameExe})";
    }

    public override bool Equals(object? obj)
    {
        if (obj is not GameModel other) return false;
        return GameName != null &&
               GameName.Equals(other.GameName, StringComparison.OrdinalIgnoreCase) &&
               GameExe.Equals(other.GameExe, StringComparison.OrdinalIgnoreCase);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(GameName?.ToLowerInvariant(), GameExe.ToLowerInvariant());
    }
}