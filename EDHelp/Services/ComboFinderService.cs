using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using EDHelp.Models;

namespace EDHelp.Services;

public class ComboFinderService : IComboFinderService
{
    private HttpClient _httpClient = new();

    public async Task<List<Combo>> FindCombosInDeck(List<Card> cards)
    {
        var query = string.Join("&", cards.Select(item =>
        {
            var encoded = Uri.EscapeDataString(item.name);
            return "c=" + encoded;
        }));

        var fullUrl = $"https://combo-finder.com/api/getCombos?{query}";

        var response = await _httpClient.GetAsync(fullUrl);
        response.EnsureSuccessStatusCode();
        string body = await response.Content.ReadAsStringAsync();
        ComboFinderResponse comboFinderResponse = JsonSerializer.Deserialize<ComboFinderResponse>(body);

        return comboFinderResponse.availableCombos;
    }
}