using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace Multi_Saves_Backup_Tool.Converters;

public class BooleanToStringConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (parameter is not string paramString)
            return value?.ToString();

        var parts = paramString.Split(';');
        if (parts.Length != 2)
            return value?.ToString();

        var boolValue = false;
        if (value is bool b)
            boolValue = b;
        else if (value is bool value1)
            boolValue = value1;

        return boolValue ? parts[0] : parts[1];
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}