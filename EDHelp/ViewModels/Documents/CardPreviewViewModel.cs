using System;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dock.Model.Mvvm.Controls;
using EDHelp.Models;
using EDHelp.Services;

namespace EDHelp.ViewModels.Documents;

public partial class CardPreviewViewModel : Document
{
    private readonly ICardCacheService _cardCacheService;
    private readonly DeckBuilderState _state;

    [ObservableProperty]
    private Card? _selectedCard;

    [ObservableProperty]
    private DeckCard? _selectedCardInDeck;

    [ObservableProperty]
    private bool _isCardPinned;

    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public object? Context { get; set; }

    public CardPreviewViewModel(ICardCacheService cardCacheService, DeckBuilderState state)
    {
        _cardCacheService = cardCacheService;
        _state = state;

        _state.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(DeckBuilderState.SelectedCard))
            {
                if (!_isCardPinned)
                {
                    SelectedCard = _state.SelectedCard;
                }
            }
        };

        _state.CardsChanged += UpdateSelectedCardInDeck;
    }

    partial void OnSelectedCardChanged(Card? value)
    {
        UpdateSelectedCardInDeck();
    }

    private void UpdateSelectedCardInDeck()
    {
        SelectedCardInDeck = _state.GetCardInDeck(SelectedCard);
    }

    [RelayCommand]
    private void PinCard()
    {
        IsCardPinned = true;
    }

    [RelayCommand]
    private void CloseCard()
    {
        SelectedCard = null;
        IsCardPinned = false;
    }

    [RelayCommand]
    private async Task AddCardToDeck(Card card)
    {
        if (card == null) return;

        var existingCard = _state.Cards.FirstOrDefault(dc => 
            dc.card.name.Equals(card.name, StringComparison.OrdinalIgnoreCase));

        if (existingCard != null)
        {
            if (card.type?.Contains("Land") == true || existingCard.Quantity < 1)
            {
                existingCard.Quantity++;
                _state.TotalCards++;
            }
        }
        else
        {
            var fetchedCard = await _cardCacheService.GetCardByName(card.name);
            if (fetchedCard != null)
            {
                var newDeckCard = new DeckCard
                {
                    card = fetchedCard,
                    Quantity = 1
                };

                _state.Cards.Add(newDeckCard);
                _state.TotalCards++;
            }
        }

        _state.NotifyCardsChanged();
        UpdateSelectedCardInDeck();
    }

    [RelayCommand]
    private void RemoveOneCardFromDeck(Card card)
    {
        if (card == null) return;

        var existingCard = _state.Cards.FirstOrDefault(dc => 
            dc.card.name.Equals(card.name, StringComparison.OrdinalIgnoreCase));
        if (existingCard == null) return;

        if (existingCard.Quantity > 1)
        {
            existingCard.Quantity--;
            _state.TotalCards--;
        }
        else
        {
            _state.Cards.Remove(existingCard);
            _state.TotalCards--;
        }

        _state.NotifyCardsChanged();
        UpdateSelectedCardInDeck();
    }

    [RelayCommand]
    private void RemoveAllCopiesFromDeck(Card card)
    {
        if (card == null) return;

        var existingCard = _state.Cards.FirstOrDefault(dc => 
            dc.card.name.Equals(card.name, StringComparison.OrdinalIgnoreCase));
        if (existingCard == null) return;

        _state.TotalCards -= existingCard.Quantity;
        _state.Cards.Remove(existingCard);

        _state.NotifyCardsChanged();
        UpdateSelectedCardInDeck();
    }
}