using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Properties;

namespace Multi_Saves_Backup_Tool.Converters;

public class EnableDisableConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool b)
            return b ? Resources.GamesView_DisableGame : Resources.GamesView_EnableGame;
        return Resources.GamesView_EnableGame;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}