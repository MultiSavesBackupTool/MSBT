using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Multi_Saves_Backup_Tool.Models;
using Properties;

namespace Multi_Saves_Backup_Tool.Converters;

public class CompressionLevelConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        try
        {
            if (value is CompressionLevel level)
                return level switch
                {
                    CompressionLevel.Fastest => Resources.CompressionLevel_Fast,
                    CompressionLevel.Optimal => Resources.CompressionLevel_Optimal,
                    CompressionLevel.SmallestSize => Resources.CompressionLevel_SmallestSize,
                    _ => level.ToString()
                };
            return null;
        }
        catch (Exception)
        {
            return value?.ToString() ?? string.Empty;
        }
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        try
        {
            if (value is string stringValue && !string.IsNullOrWhiteSpace(stringValue))
                return stringValue switch
                {
                    var s when s == Resources.CompressionLevel_Fast => CompressionLevel.Fastest,
                    var s when s == Resources.CompressionLevel_Optimal => CompressionLevel.Optimal,
                    var s when s == Resources.CompressionLevel_SmallestSize => CompressionLevel.SmallestSize,
                    _ => Enum.TryParse<CompressionLevel>(stringValue, true, out var level)
                        ? level
                        : CompressionLevel.Optimal
                };
            return CompressionLevel.Optimal;
        }
        catch (Exception)
        {
            return CompressionLevel.Optimal;
        }
    }
}