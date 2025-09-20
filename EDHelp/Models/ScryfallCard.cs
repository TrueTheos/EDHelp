using System.Collections.Generic;

namespace EDHelp.Models;

public class ScryfallCard
{
    public string name { get; set; } = "";
    public string mana_cost { get; set; } = "";
    public string type_line { get; set; } = "";
    public string oracle_text { get; set; } = "";
    public string? power { get; set; }
    public string? toughness { get; set; }
    public List<string>? colors { get; set; }
    public string rarity { get; set; } = "";
    public string set { get; set; } = "";
    public ImageUris? image_uris { get; set; }
}

public class ImageUris
{
    public string? normal { get; set; }
    public string? small { get; set; }
    public string? large { get; set; }
}