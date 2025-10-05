using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dock.Model.Controls;
using Dock.Model.Core;
using EDHelp.Models;
using EDHelp.Services;
using EDHelp.ViewModels.Documents;
using Microsoft.Extensions.DependencyInjection;

namespace EDHelp.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    private readonly IFactory _factory;
    private readonly ICardCacheService _cardCacheService;
    private readonly DecklistParser _parser;
    private readonly IServiceProvider _provider;
    private readonly DeckBuilderState _state;

    [ObservableProperty]
    private IRootDock? _layout;

    [ObservableProperty]
    private bool _isDeckLoaded;

    public string Id { get; set; } = "MainWindow";
    public string Title { get; set; } = "EDHelp - Deck Builder";
    public object? Context { get; set; }

    public MainWindowViewModel(
        IFactory factory, 
        ICardCacheService cardCacheService,
        DecklistParser parser,
        IServiceProvider provider,
        DeckBuilderState state)
    {
        _factory = factory;
        _cardCacheService = cardCacheService;
        _parser = parser;
        _provider = provider;
        _state = state;
    }

    [RelayCommand]
    private async Task ImportDeck(object? parameter)
    {
        if (parameter is not Control control) return;

        var topLevel = TopLevel.GetTopLevel(control);
        if (topLevel is null) return;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Import Deck",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("Text Files") { Patterns = new[] { "*.txt" } }
            }
        });

        if (files.Count > 0)
        {
            var filePath = files[0].Path.LocalPath;
            await ImportDeckFromFile(filePath);
        }
    }

    public async Task ImportDeckFromFile(string filePath)
    {
        try
        {
            var deck = _parser.ParseDecklistFromFile(filePath);
            await InitializeAsync(deck);
            IsDeckLoaded = true;
        }
        catch (Exception ex)
        {
            // Handle error - you might want to show a dialog or notification
            Console.WriteLine($"Error importing deck: {ex.Message}");
        }
    }

    public async Task InitializeAsync(Deck deck)
    {
        // Initialize the shared state
        _state.Commander = deck.commander;
        _state.TotalCards = deck.totalCards;

        // Fetch all cards
        var fetchedCards = await _cardCacheService.FetchDeck(deck.cards);
        _state.Cards = new System.Collections.ObjectModel.ObservableCollection<DeckCard>(fetchedCards);

        // Create dock layout
        Layout = _factory.CreateLayout();
        _factory.InitLayout(Layout);

        // Initialize the deck list view with the cards
        var deckListVm = _provider.GetRequiredService<DeckListViewModel>();
        deckListVm.Initialize(_state.Cards);

        _state.NotifyCardsChanged();
    }

    public async Task SaveLayoutAsync()
    {
        // Implement layout saving logic here if needed
        await Task.CompletedTask;
    }
}