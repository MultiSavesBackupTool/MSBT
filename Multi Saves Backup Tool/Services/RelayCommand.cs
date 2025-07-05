using System;
using System.Windows.Input;
using Microsoft.Extensions.Logging;

namespace Multi_Saves_Backup_Tool.Services;

public class RelayCommand(Action execute, Func<bool>? canExecute = null, ILogger? logger = null)
    : ICommand
{
    private readonly Action _execute = execute ?? throw new ArgumentNullException(nameof(execute));

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter)
    {
        try
        {
            return canExecute?.Invoke() ?? true;
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Error in CanExecute for RelayCommand");
            return false;
        }
    }

    public void Execute(object? parameter)
    {
        try
        {
            _execute();
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Error executing RelayCommand");
            throw;
        }
    }

    public void RaiseCanExecuteChanged()
    {
        CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}

public class RelayCommand<T>(Action<T> execute, Func<T, bool>? canExecute = null, ILogger? logger = null)
    : ICommand
{
    private readonly Action<T> _execute = execute ?? throw new ArgumentNullException(nameof(execute));

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter)
    {
        try
        {
            if (parameter is T typedParameter) return canExecute?.Invoke(typedParameter) ?? true;
            return canExecute == null;
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Error in CanExecute for RelayCommand<T>");
            return false;
        }
    }

    public void Execute(object? parameter)
    {
        try
        {
            if (parameter is T typedParameter)
                _execute(typedParameter);
            else
                logger?.LogWarning(
                    "Parameter type mismatch in RelayCommand<T>. Expected {ExpectedType}, got {ActualType}",
                    typeof(T).Name, parameter?.GetType().Name ?? "null");
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Error executing RelayCommand<T>");
            throw;
        }
    }

    public void RaiseCanExecuteChanged()
    {
        CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}