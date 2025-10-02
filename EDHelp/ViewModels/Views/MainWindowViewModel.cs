using System;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dock.Model.Controls;
using Dock.Model.Core;
using EDHelp.Services;
using Microsoft.Extensions.DependencyInjection;

namespace EDHelp.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly DecklistParser _parser;
    private readonly ICardCacheService _cardCacheService;
    private readonly IServiceProvider _serviceProvider;

    [ObservableProperty]
    private bool _isDragOver;

    [ObservableProperty]
    private string _dragText = "Drag a decklist (.txt) file here to import";

    [ObservableProperty]
    private bool _showImportView = true;

    [ObservableProperty]
    private IDockable? _currentView;

    private DockFactory? _factory;

    public DockFactory Factory
    {
        get
        {
            if (_factory == null)
            {
                _factory = new DockFactory(this, _serviceProvider);
                var layout = _factory.CreateLayout();
                _factory.InitLayout(layout);
                CurrentView = layout;
            }
            return _factory;
        }
    }

    public MainWindowViewModel(ICardCacheService cardCacheService, DecklistParser parser, IServiceProvider serviceProvider)
    {
        _parser = parser;
        _cardCacheService = cardCacheService;
        _serviceProvider = serviceProvider;
    }

    [RelayCommand]
    private async Task ImportDeck(string filePath)
    {
        try
        {
            DragText = "Importing deck...";
            var deck = _parser.ParseDecklistFromFile(filePath);

            // Initialize factory and get the DeckBuilder
            var factory = Factory; // This will create the factory if not already created
            var deckBuilder = _serviceProvider.GetRequiredService<DeckBuilderViewModel>();
            deckBuilder.InitDeck(deck);

            // Navigate to the deck builder view
            ShowImportView = false;
        }
        catch (Exception ex)
        {
            DragText = $"Error importing deck: {ex.Message}";
            await Task.Delay(3000);
            DragText = "Drag a decklist (.txt) file here to import";
        }
    }

    [RelayCommand]
    private void NavigateToDeckBuilder()
    {
        ShowImportView = false;
        _ = Factory; // Ensure factory is initialized
    }
}