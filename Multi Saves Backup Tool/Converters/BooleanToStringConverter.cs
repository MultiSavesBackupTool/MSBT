using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace Multi_Saves_Backup_Tool.Converters;

public class BooleanToStringConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool boolValue && parameter is string paramString)
        {
            string?[] options = paramString.Split('|');
            if (options.Length == 2) return boolValue ? options[0] : options[1];
        }

        return value;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}