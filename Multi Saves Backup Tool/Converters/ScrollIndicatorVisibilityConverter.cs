using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia.Data.Converters;

namespace Multi_Saves_Backup_Tool.Converters;

public class ScrollIndicatorVisibilityConverter : IMultiValueConverter
{
    public object Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Count < 3 ||
            values[0] is not double offsetY ||
            values[1] is not double extentHeight ||
            values[2] is not double viewportHeight)
            return 0.0;

        var maxScroll = extentHeight - viewportHeight;
        return maxScroll - offsetY > 5 ? 1.0 : 0.0;
    }

    public object[] ConvertBack(object? value, Type[] targetTypes, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}