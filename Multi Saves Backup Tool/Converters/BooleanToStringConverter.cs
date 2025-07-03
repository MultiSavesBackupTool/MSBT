using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Properties;

namespace Multi_Saves_Backup_Tool.Converters;

public class BooleanToStringConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool boolValue && parameter is string paramString)
        {
            string?[] options = paramString.Split('|');
            if (options.Length == 2)
            {
                var trueKey = options[0];
                var falseKey = options[1];
                if (trueKey != null)
                {
                    var trueValue = Resources.ResourceManager.GetString(trueKey, Resources.Culture) ?? trueKey;
                    if (falseKey != null)
                    {
                        var falseValue = Resources.ResourceManager.GetString(falseKey, Resources.Culture) ?? falseKey;
                        return boolValue ? trueValue : falseValue;
                    }
                }
            }
        }
        return value;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}