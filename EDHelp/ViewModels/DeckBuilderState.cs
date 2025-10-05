using System;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using EDHelp.Models;

public partial class DeckBuilderState : ObservableObject
{
    [ObservableProperty]
    private ObservableCollection<DeckCard> _cards = new();

    [ObservableProperty]
    private Card? _commander;

    [ObservableProperty]
    private Card? _selectedCard;

    [ObservableProperty]
    private int _totalCards;

    public event Action? CardsChanged;
    public event Action? CommanderChanged;

    partial void OnCommanderChanged(Card? value)
    {
        CommanderChanged?.Invoke();
    }

    partial void OnSelectedCardChanged(Card? value)
    {
        // Notify any listeners that might need to update based on selected card
    }

    public void NotifyCardsChanged()
    {
        CardsChanged?.Invoke();
    }

    // Add this method
    public void NotifyCommanderChanged()
    {
        CommanderChanged?.Invoke();
    }

    public DeckCard? GetCardInDeck(Card? card)
    {
        if (card == null) return null;
        return Cards.FirstOrDefault(dc => dc.card.name.Equals(card.name, StringComparison.OrdinalIgnoreCase));
    }
}