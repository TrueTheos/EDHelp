using System;
using System.Globalization;
using Avalonia.Data.Converters;
using EDHelp.Models;

namespace EDHelp.Converters;

public class CardActionButtonTextConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is DeckCard deckCard)
        {
            return $"Add Another Copy";
        }
            
        return "Add to Deck";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}