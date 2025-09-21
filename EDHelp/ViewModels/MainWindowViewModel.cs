using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EDHelp.Services;

namespace EDHelp.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly DecklistParser _parser;
    private readonly CardCacheService _cardCacheService;

    [ObservableProperty]
    private bool _isDragOver;

    [ObservableProperty]
    private string _dragText = "Drag a decklist (.txt) file here to import";

    [ObservableProperty]
    private bool _showImportView = true;

    [ObservableProperty]
    private DeckBuilderViewModel? _currentDeckBuilder;

    public MainWindowViewModel(CardCacheService cardCacheService, DecklistParser parser)
    {
        _parser = parser;
        _cardCacheService = cardCacheService;
    }

    [RelayCommand]
    private async Task ImportDeck(string filePath)
    {
        try
        {
            DragText = "Importing deck...";
            var deck = _parser.ParseDecklistFromFile(filePath);
                
            CurrentDeckBuilder = new DeckBuilderViewModel(deck, _cardCacheService, _parser);
            ShowImportView = false;
        }
        catch (Exception ex)
        {
            DragText = $"Error importing deck: {ex.Message}";
            await Task.Delay(3000);
            DragText = "Drag a decklist (.txt) file here to import";
        }
    }
}