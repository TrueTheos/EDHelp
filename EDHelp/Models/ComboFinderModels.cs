using System.Collections.Generic;
using Newtonsoft.Json;

namespace EDHelp.Models;

public class Combo
{
    [JsonProperty(PropertyName = "c")]
    public List<string> cards { get; set; }
    [JsonProperty(PropertyName = "p")]
    public string additionalRequirements { get; set; }
    [JsonProperty(PropertyName = "s")]
    public string instructions { get; set; }
    [JsonProperty(PropertyName = "r")]
    public string results { get; set; }
    [JsonProperty(PropertyName = "ci")]
    public Dictionary<string, string> cardImages { get; set; }
}

public class ComboFinderResponse
{
    public List<Combo> availableCombos { get; set; }
}