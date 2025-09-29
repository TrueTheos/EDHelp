using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EDHelp.Models;
using EDHelp.Services;
using Microsoft.Extensions.DependencyInjection;

namespace EDHelp.ViewModels;

public partial class DeckBuilderViewModel : ObservableObject
{
    private readonly ICardCacheService _cardCacheService;
    private readonly IMoxfieldService _moxfieldService;
    private readonly IComboFinderService _comboFinderService;
    private readonly DecklistParser _parser;

    [ObservableProperty]
    private ObservableCollection<DeckCard> _cards;

    [ObservableProperty]
    private ObservableCollection<CardTypeGroup> _groupedCards;
    
    [ObservableProperty]
    private ObservableCollection<DeckCard> _moxfieldCommonCards;

    [ObservableProperty]
    private ObservableCollection<MoxfieldDeck> _moxfieldBestDecks;

    [ObservableProperty]
    private Card? _selectedCard;
    
    [ObservableProperty]
    private DeckCard? _selectedCardInDeck;

    [ObservableProperty]
    private bool _isCardPinned;
    
    private CancellationTokenSource? _currentSearchCts;
    private readonly SemaphoreSlim _searchSemaphore = new(1, 1);

    [ObservableProperty]
    private ObservableCollection<ManaCurvePoint> _manaCurve;

    [ObservableProperty]
    private ObservableCollection<string> _suggestedCards;

    private string _searchCard;
    public string SearchCard
    {
        get => _searchCard;
        set
        {
            if (SetProperty(ref _searchCard, value))
            {
                OnCardSearchSelected();
            }
        }
    }
    
    public Card? commander { get; private set; }
    public int totalCards { get; private set; }
    
    public DeckBuilderViewModel(ICardCacheService cardCacheService, IMoxfieldService moxfieldService, IComboFinderService comboFinderService, DecklistParser parser)
    {
        _cardCacheService = cardCacheService;
        _moxfieldService = moxfieldService;
        _comboFinderService = comboFinderService;
        _parser = parser;
        _groupedCards = new ObservableCollection<CardTypeGroup>();
        _manaCurve = new ObservableCollection<ManaCurvePoint>();
    }

    public async void InitDeck(Deck deck)
    {
        commander = deck.commander;
        totalCards = deck.totalCards;
        
        _cards = new ObservableCollection<DeckCard>(deck.cards);
        
        var fetchedCards = await _cardCacheService.FetchDeck(_cards.ToList());

        _cards = new ObservableCollection<DeckCard>(fetchedCards);
        OnPropertyChanged(nameof(Cards));
        
        GroupCardsByType();
        CalculateManaCurve();
    }

    private async Task OnCardSearchSelected()
    {
        var card = await _cardCacheService.GetCardByName(_searchCard);
        ShowCardDetails(card);
    }
    
    partial void OnSelectedCardChanged(Card? value)
    {
        UpdateSelectedCardInDeck();
    }
    
    private void UpdateSelectedCardInDeck()
    {
        if (SelectedCard == null)
        {
            SelectedCardInDeck = null;
            return;
        }

        SelectedCardInDeck = _cards.FirstOrDefault(dc => dc.card.name.Equals(SelectedCard.name, StringComparison.OrdinalIgnoreCase));
    }

    private void GroupCardsByType()
    {
        var groups = _cards
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
    
    public async Task<IEnumerable<object>> UpdateSearchList(string? searchText, CancellationToken cancellationToken) 
    {
        _currentSearchCts?.Cancel();
        _currentSearchCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        
        await _searchSemaphore.WaitAsync(cancellationToken);
        
        try
        {
            await Task.Delay(150, _currentSearchCts.Token);
            
            List<string> bestChoices = await Task.Run(() => 
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

    private void CalculateManaCurve()
    {
        var curve = new Dictionary<int, int>();
        
        foreach (var deckCard in _cards)
        {
            var cmc = GetConvertedManaCost(deckCard.card.manaCost);
            if (cmc >= 7) cmc = 7;
            
            if (curve.ContainsKey(cmc))
                curve[cmc] += deckCard.Quantity;
            else
                curve[cmc] = deckCard.Quantity;
        }

        _manaCurve.Clear();
        for (int i = 0; i <= 7; i++)
        {
            _manaCurve.Add(new ManaCurvePoint
            {
                manaCost = i == 7 ? "7+" : i.ToString(),
                count = curve.ContainsKey(i) ? curve[i] : 0
            });
        }
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

    private int GetConvertedManaCost(string? manaCost)
    {
        if (string.IsNullOrEmpty(manaCost)) return 0;
        
        var cmc = 0;
        var i = 0;
        
        while (i < manaCost.Length)
        {
            if (char.IsDigit(manaCost[i]))
            {
                cmc += int.Parse(manaCost[i].ToString());
            }
            else if (manaCost[i] == '{' && i + 2 < manaCost.Length && manaCost[i + 2] == '}')
            {
                var symbol = manaCost[i + 1];
                if (char.IsDigit(symbol))
                    cmc += int.Parse(symbol.ToString());
                else if (symbol is 'W' or 'U' or 'B' or 'R' or 'G' or 'C')
                    cmc += 1;
                i += 2;
            }
            i++;
        }
        
        return cmc;
    }
    
    [RelayCommand]
    private async Task SetAsCommander(DeckCard deckCard)
    {
        if (deckCard.card.type?.Contains("Legendary") == true && 
            deckCard.card.type?.Contains("Creature") == true)
        {
            commander = deckCard.card;

            _ = UpdateMoxfieldCommondCardsAndDecks(commander.name);
            
            OnPropertyChanged(nameof(commander));
            await _cardCacheService.GetCard(deckCard.card);
        }
    }

    private async Task UpdateMoxfieldCommondCardsAndDecks(string commanderName)
    {
        var decks = await _moxfieldService.ExportTopDecksForCommander(commander.name);
        _ = UpdateCommonCards(decks);
        _ = UpdateMoxfieldDecks(decks);
    }

    [RelayCommand]
    private async Task UpdateCombos()
    {
        var combos = _comboFinderService.FindCombosInDeck(_cards.Select(x => x.card).ToList());
    }

    private async Task UpdateCommonCards(List<MoxfieldDeck> decks)
    {
        _moxfieldCommonCards = new();
        List<Deck> parsedDecks = new();
        foreach (var deck in decks)
        {
            parsedDecks.Add(_parser.ParseDeckList(deck.cards));
        }

        var commonCards = FindCommonCards(parsedDecks);
        foreach (var commonCard in commonCards)
        {
            if(commonCard.card.name is "Forest" or "Swamp" or "Island" or "Plains" or "Mountain") continue;
            MoxfieldCommonCards.Add(commonCard);
        }
            
        OnPropertyChanged(nameof(MoxfieldCommonCards));
    }

    private async Task UpdateMoxfieldDecks(List<MoxfieldDeck> decks)
    {
        _moxfieldBestDecks = new();

        foreach (var deck in decks)
        {
            MoxfieldBestDecks.Add(deck);
        }
        
        OnPropertyChanged(nameof(MoxfieldBestDecks));
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
    
    private async Task ShowCardDetails(Card card)
    { 
        var cardData = await _cardCacheService.GetCard(card);
        
        SelectedCard = cardData;
        IsCardPinned = false;
        
        OnPropertyChanged(nameof(SelectedCard));
    }

    [RelayCommand]
    private void RemoveCard(DeckCard deckCard)
    {
        var group = _groupedCards.FirstOrDefault(g => g.cards.Contains(deckCard));
        group?.cards.Remove(deckCard);
        _cards.Remove(deckCard);
        
        totalCards = _cards.Sum(dc => dc.Quantity);
        OnPropertyChanged(nameof(totalCards));
        CalculateManaCurve();
        
        if (_groupedCards.Any(g => g.cards.Count == 0))
        {
            GroupCardsByType();
        }
    }

    [RelayCommand]
    private void IncreaseQuantity(DeckCard deckCard)
    {
        deckCard.Quantity++;
        totalCards++;
        OnPropertyChanged(nameof(totalCards));
        CalculateManaCurve();
            
        var group = _groupedCards.FirstOrDefault(g => g.cards.Contains(deckCard));
        if (group != null)
        {
            group.count = group.cards.Sum(dc => dc.Quantity);
            OnPropertyChanged(nameof(GroupedCards));
        }
        
        UpdateSelectedCardInDeck();
    }

    [RelayCommand]
    private void DecreaseQuantity(DeckCard deckCard)
    {
        if (deckCard.Quantity > 1)
        {
            deckCard.Quantity--;
            totalCards--;
            OnPropertyChanged(nameof(totalCards));
            CalculateManaCurve();
            
            var group = _groupedCards.FirstOrDefault(g => g.cards.Contains(deckCard));
            if (group != null)
            {
                group.count = group.cards.Sum(dc => dc.Quantity);
                OnPropertyChanged(nameof(GroupedCards));
            }
        }
        else
        {
            RemoveCard(deckCard);
        }
        
        UpdateSelectedCardInDeck();
    }
    
    [RelayCommand]
    private async Task ShowCardDetails(DeckCard deckCard)
    {
        await ShowCardDetails(deckCard.card);
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
    private void OpenMoxfieldDeck(string link)
    {
        Process.Start(new ProcessStartInfo(link) { UseShellExecute = true });
    }

    [RelayCommand]
    private void ToggleGroup(CardTypeGroup group)
    {
        group.IsExpanded = !group.IsExpanded;
    }
    
    [RelayCommand]
    private async Task AddCardToDeck(Card card)
    {
        if (card == null) return;

        var existingCard = _cards.FirstOrDefault(dc => dc.card.name.Equals(card.name, StringComparison.OrdinalIgnoreCase));
        
        if (existingCard != null)
        {
            // Card already in deck, increase quantity
            if (card.type?.Contains("Land") == true || existingCard.Quantity < 1)
            {
                existingCard.Quantity++;
                totalCards++;
                
                // Update the group count
                var group = _groupedCards.FirstOrDefault(g => g.cards.Contains(existingCard));
                if (group != null)
                {
                    group.count = group.cards.Sum(dc => dc.Quantity);
                }
            }
        }
        else
        {
            // New card, add to deck
            var fetchedCard = await _cardCacheService.GetCardByName(card.name);
            if (fetchedCard != null)
            {
                var newDeckCard = new DeckCard
                {
                    card = fetchedCard,
                    Quantity = 1
                };
                
                _cards.Add(newDeckCard);
                totalCards++;
                
                // Regroup cards to place new card in correct category
                GroupCardsByType();
            }
        }
        
        OnPropertyChanged(nameof(totalCards));
        CalculateManaCurve();
        UpdateSelectedCardInDeck();
    }

    [RelayCommand]
    private void RemoveOneCardFromDeck(Card card)
    {
        if (card == null) return;

        var existingCard = _cards.FirstOrDefault(dc => dc.card.name.Equals(card.name, StringComparison.OrdinalIgnoreCase));
        if (existingCard == null) return;

        if (existingCard.Quantity > 1)
        {
            existingCard.Quantity--;
            totalCards--;
            
            // Update the group count
            var group = _groupedCards.FirstOrDefault(g => g.cards.Contains(existingCard));
            if (group != null)
            {
                group.count = group.cards.Sum(dc => dc.Quantity);
            }
        }
        else
        {
            // Remove the card completely
            var group = _groupedCards.FirstOrDefault(g => g.cards.Contains(existingCard));
            group?.cards.Remove(existingCard);
            _cards.Remove(existingCard);
            totalCards--;
            
            // Clean up empty groups
            if (_groupedCards.Any(g => g.cards.Count == 0))
            {
                GroupCardsByType();
            }
        }
        
        OnPropertyChanged(nameof(totalCards));
        CalculateManaCurve();
        UpdateSelectedCardInDeck();
    }

    [RelayCommand]
    private void RemoveAllCopiesFromDeck(Card card)
    {
        if (card == null) return;

        var existingCard = _cards.FirstOrDefault(dc => dc.card.name.Equals(card.name, StringComparison.OrdinalIgnoreCase));
        if (existingCard == null) return;

        totalCards -= existingCard.Quantity;
        
        // Remove from group and deck
        var group = _groupedCards.FirstOrDefault(g => g.cards.Contains(existingCard));
        group?.cards.Remove(existingCard);
        _cards.Remove(existingCard);
        
        // Clean up empty groups
        if (_groupedCards.Any(g => g.cards.Count == 0))
        {
            GroupCardsByType();
        }
        
        OnPropertyChanged(nameof(totalCards));
        CalculateManaCurve();
        UpdateSelectedCardInDeck();
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

public class ManaCurvePoint
{
    public string manaCost { get; set; } = string.Empty;
    public int count { get; set; }
}