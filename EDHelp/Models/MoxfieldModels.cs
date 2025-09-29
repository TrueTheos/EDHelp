using System.Collections.Generic;
using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace EDHelp.Models;

public class MoxfieldCardSearchResponse
{
    public List<MoxfieldCardData> data { get; set; }
}
    
public class MoxfieldCardData
{
    public string id { get; set; }
        
    public string name { get; set; }
}
    
public class MoxfieldDeckSearchResponse
{
    public List<MoxfieldDeckSearchResult> data { get; set; }
        
}
    
public class MoxfieldDeckSearchResult
{
    public string name { get; set; }
    public int viewCount { get; set; }
    [JsonProperty(PropertyName = "publicUrl")]
    public string link { get; set; }
}

public class MoxfieldDeck
{
    public string name { get; set; }
    public string link { get; set; }
    public List<string> cards { get; set; }
}