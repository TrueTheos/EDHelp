using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using EDHelp.ViewModels;

namespace EDHelp.Controls;

public partial class ManaCostControl : UserControl
{
    public static readonly StyledProperty<string?> ManaCostProperty =
        AvaloniaProperty.Register<ManaCostControl, string?>(nameof(ManaCost));

    public string? ManaCost
    {
        get => GetValue(ManaCostProperty);
        set => SetValue(ManaCostProperty, value);
    }

    public ManaCostControl()
    {
        InitializeComponent();
        PropertyChanged += (sender, e) =>
        {
            if (e.Property == ManaCostProperty)
            {
                UpdateManaSymbols();
            }
        };
    }

    private void UpdateManaSymbols()
    {
        var symbols = ParseManaCost(ManaCost);
        ManaSymbolsPanel.ItemsSource = symbols;
    }

    private List<ManaSymbolViewModel> ParseManaCost(string? manaCost)
    {
        var symbols = new List<ManaSymbolViewModel>();
        
        if (string.IsNullOrEmpty(manaCost))
            return symbols;

        // Parse mana cost using regex to find {X} patterns
        var matches = Regex.Matches(manaCost, @"\{([^}]+)\}");
        
        foreach (Match match in matches)
        {
            var symbol = match.Groups[1].Value;
            symbols.Add(CreateManaSymbol(symbol));
        }

        // Handle simple numeric costs without braces
        if (!matches.Any() && int.TryParse(manaCost, out int cost))
        {
            symbols.Add(CreateManaSymbol(cost.ToString()));
        }

        return symbols;
    }

    private ManaSymbolViewModel CreateManaSymbol(string symbol)
    {
        return symbol.ToUpper() switch
        {
            "W" => new ManaSymbolViewModel("W", Brushes.White, Brushes.Black),
            "U" => new ManaSymbolViewModel("U", new SolidColorBrush(Color.FromRgb(14, 104, 171)), Brushes.White),
            "B" => new ManaSymbolViewModel("B", new SolidColorBrush(Color.FromRgb(21, 11, 0)), Brushes.White),
            "R" => new ManaSymbolViewModel("R", new SolidColorBrush(Color.FromRgb(211, 32, 42)), Brushes.White),
            "G" => new ManaSymbolViewModel("G", new SolidColorBrush(Color.FromRgb(0, 115, 62)), Brushes.White),
            "C" => new ManaSymbolViewModel("C", new SolidColorBrush(Color.FromRgb(202, 197, 192)), Brushes.Black),
            var s when int.TryParse(s, out int num) => new ManaSymbolViewModel(num.ToString(), 
                new SolidColorBrush(Color.FromRgb(202, 197, 192)), Brushes.Black),
            _ => new ManaSymbolViewModel(symbol, new SolidColorBrush(Color.FromRgb(202, 197, 192)), Brushes.Black)
        };
    }
}