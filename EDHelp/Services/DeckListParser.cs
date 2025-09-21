using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using EDHelp.Models;

namespace EDHelp.Services;

public class DecklistParser
{
    private static readonly Regex CardLineRegex = new(@"^(?:(\d+)x?\s+)?(.+?)(?:\s+\([^)]+\)\s+\d+.*)?$", RegexOptions.Compiled);
    
    public Deck ParseDecklistFromFile(string filePath)
    {
        return ParseDeckList(File.ReadAllText(filePath));
    }

    public Deck ParseDeckList(string deckList)
    {
        var lines = deckList.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
        return ParseDeckList(lines.ToList());
    }
    
    public Deck ParseDeckList(List<string> lines)
    {
        Deck deck = new();
        
        foreach (var line in lines)
        {
            var trimmedLine = line.Trim();
            
            if (string.IsNullOrEmpty(trimmedLine) || 
                trimmedLine.StartsWith("//") || 
                trimmedLine.StartsWith("#"))
                continue;
            
            var cardInfo = ParseCardLine(trimmedLine);
            if (cardInfo != null)
            {
                var card = new Card { name = cardInfo.cardName };
                deck.cards.Add(new DeckCard { card = card, quantity = cardInfo.quantity });
            }
            else
            {
                Console.WriteLine($"Card was null: {line}");
            }
        }

        return deck;
    }

    private bool IsHeaderLine(string line)
    {
        var lowerLine = line.ToLower();
        return lowerLine.Contains("commander") ||
               lowerLine.Contains("deck") ||
               lowerLine.Contains("main") ||
               lowerLine.Contains("sideboard") ||
               lowerLine.Contains("maybeboard") ||
               line.All(c => char.IsLetter(c) || char.IsWhiteSpace(c)) && line.Length < 20;
    }

    private CardInfo ParseCardLine(string line)
    {
        var match = CardLineRegex.Match(line);
        if (!match.Success)
        {
            Console.WriteLine($"Failed to parse card: {line}");
            return null;
        }

        var quantityGroup = match.Groups[1];
        var cardNameGroup = match.Groups[2];

        if (string.IsNullOrEmpty(cardNameGroup.Value))
        { 
            Console.WriteLine($"Failed at group match card: {line}");
            return null;
        }

        int quantity = 1;
        if (quantityGroup.Success && !string.IsNullOrEmpty(quantityGroup.Value))
        {
            if (!int.TryParse(quantityGroup.Value, out quantity))
                quantity = 1;
        }

        var cardName = cardNameGroup.Value.Trim();
        
        cardName = CleanCardName(cardName);

        if(string.IsNullOrEmpty(cardName)) Console.WriteLine($"Smth went wrong: {line}");
        
        return new CardInfo { quantity = quantity, cardName = cardName };
    }

    private string CleanCardName(string cardName)
    {
        cardName = Regex.Replace(cardName, @"\s+\*[A-Z]\*\s*$", "", RegexOptions.IgnoreCase);
        cardName = Regex.Replace(cardName, @"\s+★\s*$", "");
        
        cardName = Regex.Replace(cardName, @"\s+\([A-Z0-9]+\)\s+\d+.*$", "", RegexOptions.IgnoreCase);
        
        return cardName.Trim();
    }

    private class CardInfo
    {
        public int quantity { get; set; }
        public string cardName { get; set; }
    }
}