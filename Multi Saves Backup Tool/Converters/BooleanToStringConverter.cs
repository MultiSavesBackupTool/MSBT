using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Properties;

namespace Multi_Saves_Backup_Tool.Converters;

public class BooleanToStringConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        try
        {
            if (value is bool boolValue && parameter is string paramString && !string.IsNullOrWhiteSpace(paramString))
            {
                string?[] options = paramString.Split('|');
                if (options.Length == 2)
                {
                    var trueKey = options[0];
                    var falseKey = options[1];

                    if (!string.IsNullOrWhiteSpace(trueKey) && !string.IsNullOrWhiteSpace(falseKey))
                        try
                        {
                            var trueValue = Resources.ResourceManager.GetString(trueKey, Resources.Culture) ?? trueKey;
                            var falseValue = Resources.ResourceManager.GetString(falseKey, Resources.Culture) ??
                                             falseKey;
                            return boolValue ? trueValue : falseValue;
                        }
                        catch
                        {
                            return boolValue ? trueKey : falseKey;
                        }
                }
            }

            return value?.ToString() ?? string.Empty;
        }
        catch (Exception)
        {
            return value?.ToString() ?? string.Empty;
        }
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException("ConvertBack is not implemented for BooleanToStringConverter");
    }
}