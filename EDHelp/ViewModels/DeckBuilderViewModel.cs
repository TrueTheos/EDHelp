using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
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
    private readonly DecklistParser _parser;

    [ObservableProperty]
    private ObservableCollection<DeckCard> _cards;

    [ObservableProperty]
    private ObservableCollection<CardTypeGroup> _groupedCards;
    
    [ObservableProperty]
    private ObservableCollection<DeckCard> _moxfieldCommonCards;

    [ObservableProperty]
    private Card? _selectedCard;

    [ObservableProperty]
    private bool _isCardPinned;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private ObservableCollection<ManaCurvePoint> _manaCurve;
    
    public Card? commander { get; private set; }
    public int totalCards { get; private set; }
    
    public DeckBuilderViewModel(ICardCacheService cardCacheService, IMoxfieldService moxfieldService, DecklistParser parser)
    {
        _cardCacheService = cardCacheService;
        _moxfieldService = moxfieldService;
        _parser = parser;
        _groupedCards = new ObservableCollection<CardTypeGroup>();
        _manaCurve = new ObservableCollection<ManaCurvePoint>();
        
        PropertyChanged += OnPropertyChanged;
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

    private void OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SearchText))
        {
            FilterCards();
        }
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

    private void FilterCards()
    {
        if (string.IsNullOrWhiteSpace(SearchText))
        {
            GroupCardsByType();
            return;
        }

        var filteredCards = _cards
            .Where(dc => dc.card.name.Contains(SearchText, StringComparison.OrdinalIgnoreCase))
            .ToList();

        var groups = filteredCards
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

            _moxfieldCommonCards = new();

            var topDecks = await _moxfieldService.ExportTopDecksForCommander(commander.name);
            List<Deck> parsedDecks = new();
            foreach (var deck in topDecks)
            {
                parsedDecks.Add(_parser.ParseDeckList(deck));
            }

            var commonCards = FindCommonCards(parsedDecks);
            foreach (var commonCard in commonCards)
            {
                if(commonCard.card.name is "Forest" or "Swamp" or "Island" or "Plains" or "Mountain") continue;
                MoxfieldCommonCards.Add(commonCard);
            }
            
            OnPropertyChanged(nameof(MoxfieldCommonCards));
            
            OnPropertyChanged(nameof(commander));
            
            await _cardCacheService.GetCard(deckCard.card);
        }
    }

    [RelayCommand]
    private void RemoveCard(DeckCard deckCard)
    {
        if (deckCard.Quantity > 1)
        {
            deckCard.Quantity--;
        }
        else
        {
            var group = _groupedCards.FirstOrDefault(g => g.cards.Contains(deckCard));
            group?.cards.Remove(deckCard);
            _cards.Remove(deckCard);
        }
        
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
        if (deckCard.card.type?.Contains("Land") == true)
        {
            deckCard.Quantity++;
            totalCards++;
            OnPropertyChanged(nameof(totalCards));
            CalculateManaCurve();
            
            var group = _groupedCards.FirstOrDefault(g => g.cards.Contains(deckCard));
            if (group != null)
            {
                group.count = group.cards.Sum(dc => dc.Quantity);
            }
        }
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
            }
        }
    }

    [RelayCommand]
    private async Task ShowCardDetails(DeckCard deckCard)
    {
        SelectedCard = deckCard.card;
        IsCardPinned = false;
        
        await _cardCacheService.GetCard(deckCard.card);
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
    private void ToggleGroup(CardTypeGroup group)
    {
        group.IsExpanded = !group.IsExpanded;
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