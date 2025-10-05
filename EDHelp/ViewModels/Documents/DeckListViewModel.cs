using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dock.Model.Mvvm.Controls;
using EDHelp.Models;
using EDHelp.Services;

namespace EDHelp.ViewModels.Documents;

public partial class DeckListViewModel : Document
{
    private readonly ICardCacheService _cardCacheService;
    private readonly DeckBuilderState _state;

    [ObservableProperty]
    private ObservableCollection<CardTypeGroup> _groupedCards;

    [ObservableProperty]
    private string _searchCard;

    private CancellationTokenSource? _currentSearchCts;
    private readonly SemaphoreSlim _searchSemaphore = new(1, 1);

    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public object? Context { get; set; }

    public DeckListViewModel(ICardCacheService cardCacheService, DeckBuilderState state)
    {
        _cardCacheService = cardCacheService;
        _state = state;
        _groupedCards = new ObservableCollection<CardTypeGroup>();

        _state.CardsChanged += OnCardsChanged;
    }

    private void OnCardsChanged()
    {
        GroupCardsByType();
    }

    public void Initialize(ObservableCollection<DeckCard> cards)
    {
        _state.Cards = cards;
        GroupCardsByType();
    }

    private void GroupCardsByType()
    {
        var groups = _state.Cards
            .GroupBy(dc => GetCardTypeCategory(dc.card))
            .OrderBy(g => GetTypeOrder(g.Key))
            .Select(g => new CardTypeGroup
            {
                typeName = g.Key,
                cards = new ObservableCollection<DeckCard>(g.OrderBy(dc => dc.card.name)),
                count = g.Sum(dc => dc.Quantity)
            });

        _groupedCards.Clear();
        foreach (var group in groups)
        {
            _groupedCards.Add(group);
        }
    }

    public async Task<System.Collections.Generic.IEnumerable<object>> UpdateSearchList(
        string? searchText, 
        CancellationToken cancellationToken)
    {
        _currentSearchCts?.Cancel();
        _currentSearchCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        await _searchSemaphore.WaitAsync(cancellationToken);

        try
        {
            await Task.Delay(150, _currentSearchCts.Token);

            var bestChoices = await Task.Run(() =>
                    _cardCacheService.FindBestCardNameMatches(searchText),
                _currentSearchCts.Token);

            return bestChoices;
        }
        catch (OperationCanceledException)
        {
            return Enumerable.Empty<object>();
        }
        finally
        {
            _searchSemaphore.Release();
        }
    }

    partial void OnSearchCardChanged(string value)
    {
        _ = OnCardSearchSelected();
    }

    private async Task OnCardSearchSelected()
    {
        if (string.IsNullOrWhiteSpace(_searchCard)) return;

        var card = await _cardCacheService.GetCardByName(_searchCard);
        if (card != null)
        {
            _state.SelectedCard = card;
        }
    }

    [RelayCommand]
    private void IncreaseQuantity(DeckCard deckCard)
    {
        deckCard.Quantity++;
        _state.TotalCards++;

        var group = _groupedCards.FirstOrDefault(g => g.cards.Contains(deckCard));
        if (group != null)
        {
            group.count = group.cards.Sum(dc => dc.Quantity);
        }

        _state.NotifyCardsChanged();
    }

    [RelayCommand]
    private void DecreaseQuantity(DeckCard deckCard)
    {
        if (deckCard.Quantity > 1)
        {
            deckCard.Quantity--;
            _state.TotalCards--;

            var group = _groupedCards.FirstOrDefault(g => g.cards.Contains(deckCard));
            if (group != null)
            {
                group.count = group.cards.Sum(dc => dc.Quantity);
            }

            _state.NotifyCardsChanged();
        }
        else
        {
            RemoveCard(deckCard);
        }
    }

    [RelayCommand]
    private void RemoveCard(DeckCard deckCard)
    {
        var group = _groupedCards.FirstOrDefault(g => g.cards.Contains(deckCard));
        group?.cards.Remove(deckCard);
        _state.Cards.Remove(deckCard);

        _state.TotalCards = _state.Cards.Sum(dc => dc.Quantity);

        if (_groupedCards.Any(g => g.cards.Count == 0))
        {
            GroupCardsByType();
        }

        _state.NotifyCardsChanged();
    }

    [RelayCommand]
    private void ShowCardDetails(DeckCard deckCard)
    {
        _state.SelectedCard = deckCard.card;
    }

    [RelayCommand]
    private async Task SetAsCommander(DeckCard deckCard)
    {
        if (deckCard.card.type?.Contains("Legendary") == true &&
            deckCard.card.type?.Contains("Creature") == true)
        {
            _state.Commander = deckCard.card;
            await _cardCacheService.GetCard(deckCard.card);
        }
    }

    [RelayCommand]
    private void ToggleGroup(CardTypeGroup group)
    {
        group.IsExpanded = !group.IsExpanded;
    }

    private string GetCardTypeCategory(Card card)
    {
        var types = card.type?.ToLower() ?? "";

        if (types.Contains("creature")) return "Creatures";
        if (types.Contains("planeswalker")) return "Planeswalkers";
        if (types.Contains("instant")) return "Instants";
        if (types.Contains("sorcery")) return "Sorceries";
        if (types.Contains("enchantment")) return "Enchantments";
        if (types.Contains("artifact")) return "Artifacts";
        if (types.Contains("land")) return "Lands";

        return "Other";
    }

    private int GetTypeOrder(string typeName)
    {
        return typeName switch
        {
            "Creatures" => 0,
            "Planeswalkers" => 1,
            "Instants" => 2,
            "Sorceries" => 3,
            "Enchantments" => 4,
            "Artifacts" => 5,
            "Lands" => 6,
            "Other" => 7,
            _ => 8
        };
    }
}

public partial class CardTypeGroup : ObservableObject
{
    public string typeName { get; set; } = string.Empty;
    public ObservableCollection<DeckCard> cards { get; set; } = new();
    public int count { get; set; }

    [ObservableProperty]
    private bool _isExpanded = true;
}