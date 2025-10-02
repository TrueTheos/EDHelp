using Avalonia.Media;

namespace EDHelp.ViewModels;

public class ManaSymbolViewModel
{
    public string Symbol { get; }
    public IBrush Background { get; }
    public IBrush Foreground { get; }

    public ManaSymbolViewModel(string symbol, IBrush background, IBrush foreground)
    {
        Symbol = symbol;
        Background = background;
        Foreground = foreground;
    }
}