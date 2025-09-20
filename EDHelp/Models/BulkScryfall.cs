using System.Collections.Generic;

namespace EDHelp.Models;

public class BulkCardRequest
{
    public string name { get; set; } = string.Empty;
    public string? set { get; set; }
    public string? collector_number { get; set; }
}

public class BulkCardResponse
{
    public List<ScryfallCard> data { get; set; } = new();
    public List<BulkCardRequest> not_found { get; set; } = new();
}