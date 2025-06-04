using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Multi_Saves_Backup_Tool.Models;

namespace Multi_Saves_Backup_Tool.Converters;

public class CompressionLevelConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is CompressionLevel level)
        {
            return level switch
            {
                CompressionLevel.Fastest => "Быстрый",
                CompressionLevel.Optimal => "Оптимальный",
                CompressionLevel.SmallestSize => "Максимальный",
                _ => level.ToString()
            };
        }
        return null;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string stringValue)
        {
            return stringValue switch
            {
                "Быстрый" => CompressionLevel.Fastest,
                "Оптимальный" => CompressionLevel.Optimal,
                "Максимальный" => CompressionLevel.SmallestSize,
                _ => Enum.TryParse<CompressionLevel>(stringValue, out var level) ? level : null
            };
        }
        return null;
    }
}
