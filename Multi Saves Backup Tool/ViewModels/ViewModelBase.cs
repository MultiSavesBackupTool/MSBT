using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;

namespace Multi_Saves_Backup_Tool.ViewModels;

public abstract class ViewModelBase(ILogger? logger = null) : ObservableObject
{
    private readonly ILogger? _logger = logger;

    protected virtual void LogError(Exception ex, string message, params object?[] args)
    {
        _logger?.LogError(ex, message, args);
    }

    protected virtual void LogWarning(string message, params object[] args)
    {
        _logger?.LogWarning(message, args);
    }

    protected virtual void LogInformation(string message, params object?[] args)
    {
        _logger?.LogInformation(message, args);
    }

    protected virtual void LogDebug(string message, params object[] args)
    {
        _logger?.LogDebug(message, args);
    }

    protected new virtual bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    protected virtual void RaisePropertyChanged([CallerMemberName] string? propertyName = null)
    {
        OnPropertyChanged(propertyName);
    }

    protected virtual void RaisePropertyChanged(params string[] propertyNames)
    {
        foreach (var propertyName in propertyNames) OnPropertyChanged(propertyName);
    }
}