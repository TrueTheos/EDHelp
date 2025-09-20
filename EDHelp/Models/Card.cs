namespace EDHelp.Models;

public class Card
{
    public int id { get; set; }
    public string name { get; set; } = string.Empty;
    public string manaCost { get; set; } = string.Empty;
    public int convertedManaCost { get; set; }
    public string type { get; set; } = string.Empty;
    public string colors { get; set; } = string.Empty;
    public string text { get; set; } = string.Empty;
    public string imageUrl { get; set; } = string.Empty;
    public bool isCommander { get; set; }
}