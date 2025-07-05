using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;

namespace Multi_Saves_Backup_Tool.Models;

public class GameModel : INotifyPropertyChanged
{
    private string? _addPath;
    private int _backupCount;
    private int _backupInterval = 60;
    private int _daysForKeep;
    private string _gameExe = string.Empty;
    private string? _gameExeAlt;
    private string _gameName = string.Empty;
    private bool _isEnabled = true;
    private string? _modPath;
    private string? _savePath = string.Empty;
    private int _setOldFilesStatus;
    private bool _specialBackupMark;

    public string GameName
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
            if (value <= 0)
                throw new ArgumentException("Backup interval must be greater than 0", nameof(value));
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

    public bool IsValid()
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
            SpecialBackupMark = SpecialBackupMark
        };
    }

    public override string ToString()
    {
        return $"{GameName} ({GameExe})";
    }

    public override bool Equals(object? obj)
    {
        if (obj is not GameModel other) return false;
        return GameName.Equals(other.GameName, StringComparison.OrdinalIgnoreCase) &&
               GameExe.Equals(other.GameExe, StringComparison.OrdinalIgnoreCase);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(GameName.ToLowerInvariant(), GameExe.ToLowerInvariant());
    }
}