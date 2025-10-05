using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dock.Model.Mvvm.Controls;
using EDHelp.Models;
using EDHelp.Services;

namespace EDHelp.ViewModels.Tools;

public partial class CommonCardsViewModel : Tool
{
    private readonly IMoxfieldService _moxfieldService;
    private readonly DecklistParser _parser;
    private readonly DeckBuilderState _state;

    [ObservableProperty]
    private ObservableCollection<DeckCard> _commonCards = new();

    [ObservableProperty]
    private bool _isLoading;

    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public object? Context { get; set; }

    public CommonCardsViewModel(
        IMoxfieldService moxfieldService, 
        DecklistParser parser,
        DeckBuilderState state)
    {
        _moxfieldService = moxfieldService;
        _parser = parser;
        _state = state;

        _state.CommanderChanged += OnCommanderChanged;
    }

    private async void OnCommanderChanged()
    {
        if (_state.Commander != null)
        {
            await LoadCommonCards(_state.Commander.name);
        }
    }

    public async Task LoadCommonCards(string commanderName)
    {
        IsLoading = true;
        try
        {
            var decks = await _moxfieldService.ExportTopDecksForCommander(commanderName);

            var parsedDecks = new List<Deck>();
            foreach (var deck in decks)
            {
                parsedDecks.Add(_parser.ParseDeckList(deck.cards));
            }

            var commonCardsList = FindCommonCards(parsedDecks);

            _commonCards.Clear();
            foreach (var card in commonCardsList)
            {
                // Skip basic lands
                if (card.card.name is "Forest" or "Swamp" or "Island" or "Plains" or "Mountain")
                    continue;

                _commonCards.Add(card);
            }
        }
        finally
        {
            IsLoading = false;
        }
    }

    private IOrderedEnumerable<DeckCard> FindCommonCards(List<Deck> decks)
    {
        var counts = new Dictionary<string, int>();
        foreach (var deck in decks)
        {
            foreach (var deckCard in deck.cards)
            {
                counts[deckCard.card.name] = counts.GetValueOrDefault(deckCard.card.name) + 1;
            }
        }

        return counts
            .Select(kv => new DeckCard
            {
                card = new Card { name = kv.Key },
                Quantity = kv.Value
            })
            .OrderByDescending(dc => dc.Quantity)
            .ThenBy(dc => dc.card.name);
    }

    [RelayCommand]
    private void ShowCardDetails(DeckCard deckCard)
    {
        _state.SelectedCard = deckCard.card;
    }
}