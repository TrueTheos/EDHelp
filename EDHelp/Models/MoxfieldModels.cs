using System.Collections.Generic;
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
    public List<MoxfieldDeckData> data { get; set; }
        
    public int totalResults { get; set; }
}
    
public class MoxfieldDeckData
{
    public string name { get; set; }
        
    public int viewCount { get; set; }
        
    public int likeCount { get; set; }
        
    public string publicUrl { get; set; }
}
    
public class MoxfieldDeckSearchResult
{
    public string name { get; set; }
    public int views { get; set; }
    public int likes { get; set; }
    public string link { get; set; }
}