using System;
using System.Collections.Generic;
using System.Linq;

namespace EDHelp.Models;

public class Deck
{
    public int id { get; set; }
    public string name { get; set; } = string.Empty;
    public Card? commander { get; set; }
    public List<DeckCard> cards { get; set; } = new();
    public DateTime created { get; set; } = DateTime.Now;
        
    public int totalCards { get; set; }
}
    
public class DeckCard
{
    public Card card { get; set; } = null!;
    public int quantity { get; set; }
}