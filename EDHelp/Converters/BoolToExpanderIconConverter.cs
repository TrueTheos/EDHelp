using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace EDHelp.Converters;

public class BoolToExpanderIconConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isExpanded)
        {
            return isExpanded ? "⌄" : "⌃";
        }

        return "⌃"; // default fallback
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}