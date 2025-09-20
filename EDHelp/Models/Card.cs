using System;
using System.Collections.Generic;

namespace EDHelp.Models;

public class Card
{
    public string name { get; set; } = "";
    public string manaCost { get; set; } = "";
    public string type { get; set; } = "";
    public string text { get; set; } = "";
    public string? power { get; set; }
    public string? toughness { get; set; }
    public byte[]? imageData { get; set; }
    public List<string> colors { get; set; } = new();
    public string rarity { get; set; } = "";
    public string setCode { get; set; } = "";
    public DateTime cachedAt { get; set; }
}