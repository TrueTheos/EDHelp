using System;
using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;

namespace EDHelp.Models;

public class Deck
{
    public int id { get; set; }
    public Card? commander { get; set; }
    public List<DeckCard> cards { get; set; } = new();
    public int totalCards { get; set; }
}
    
public partial class DeckCard : ObservableObject
{
    public Card card { get; set; } = null!;

    [ObservableProperty] private int _quantity;
}