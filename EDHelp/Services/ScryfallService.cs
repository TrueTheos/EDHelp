using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using EDHelp.Models;

namespace EDHelp.Services;

public class ScryfallService : IScryfallService
{
    private HttpClient _httpClient = new();

    public ScryfallService()
    {
        if (!_httpClient.DefaultRequestHeaders.Contains("User-Agent"))
        {
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "EDHelp/1.0");
        }
        
        if (!_httpClient.DefaultRequestHeaders.Contains("Accept"))
        {
            _httpClient.DefaultRequestHeaders.Add("Accept", "application/json;q=0.9,*/*;q=0.8");
        }
    }

    public async Task<List<string>> GetAllCardNames()
    {
        var response = await _httpClient.GetAsync("https://api.scryfall.com/catalog/card-names");

        if (response.IsSuccessStatusCode)
        {
            var responseJson = await response.Content.ReadAsStringAsync();

            return JsonSerializer.Deserialize<ScryfallCardNames>(responseJson).data.ToList();
        }

        return null;
    }
    
    public async Task<Dictionary<string, Card>> FetchCardBatch(List<Card> cards)
    {
        var result = new Dictionary<string, Card>();
        
        var requestPayload = new
        {
            identifiers = cards.Select(card => new BulkCardRequest
            {
                name = card.name
            }).ToArray()
        };
        
        var json = JsonSerializer.Serialize(requestPayload, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
        
        var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        
        var response = await _httpClient.PostAsync("https://api.scryfall.com/cards/collection", content);
        
        if (response.IsSuccessStatusCode)
        {
            var responseJson = await response.Content.ReadAsStringAsync();
            var bulkResponse = JsonSerializer.Deserialize<BulkCardResponse>(responseJson, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            if (bulkResponse?.data != null)
            {
                foreach (var scryfallCard in bulkResponse.data)
                {
                    var imageData = await DownloadImageAsync(scryfallCard.image_uris?.normal ?? "");

                    var Card = new Card
                    {
                        name = scryfallCard.name,
                        manaCost = scryfallCard.mana_cost ?? "",
                        type = scryfallCard.type_line ?? "",
                        text = scryfallCard.oracle_text ?? "",
                        power = scryfallCard.power ?? "",
                        toughness = scryfallCard.toughness ?? "",
                        imageData = imageData,
                        colors = scryfallCard.colors ?? new List<string>(),
                        rarity = scryfallCard.rarity ?? "common",
                        setCode = scryfallCard.set ?? "",
                        cachedAt = DateTime.UtcNow
                    };

                    result[scryfallCard.name] = Card;
                }

                if (bulkResponse.not_found != null)
                {
                    foreach (var notFound in bulkResponse.not_found)
                    {
                        var originalCard = cards.FirstOrDefault(c => c.name == notFound.name);
                        if (originalCard != null)
                        {
                            result[notFound.name] = CreateFallbackCard(originalCard);
                        }
                    }
                }
            }

            return result;
        }
        else
        {
            throw new HttpRequestException($"Bulk request failed with status: {response.StatusCode}");
        }
    }
    
    public async Task<Dictionary<string, Card>> FetchCardsBulkFromApi(List<DeckCard> deckCards)
    {
        var result = new Dictionary<string, Card>();
    
        const int batchSize = 75;
        var batches = deckCards
            .Where(dc => dc.card != null)
            .Select(dc => dc.card!)
            .Distinct()
            .Select((card, index) => new { card, index })
            .GroupBy(x => x.index / batchSize)
            .Select(g => g.Select(x => x.card).ToList())
            .ToList();
    
        foreach (var batch in batches)
        {
            try
            {
                var batchResults = await FetchCardBatch(batch);
                foreach (var kvp in batchResults)
                {
                    result[kvp.Key] = kvp.Value;
                }
            
                await Task.Delay(100);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error fetching batch: {ex.Message}");
            
                foreach (var card in batch)
                {
                    try
                    {
                        await Task.Delay(75);
                        var individualResult = await FetchCardAsync(card);
                        if (individualResult != null)
                        {
                            result[card.name] = individualResult;
                        }
                    }
                    catch (Exception individualEx)
                    {
                        Console.WriteLine($"Error fetching individual card {card.name}: {individualEx.Message}");
                    }
                }
            }
        }
    
        return result;
    }
    
    public async Task<Card?> FetchCardAsync(Card card)
    {
        try
        {
            var encodedName = Uri.EscapeDataString(card.name);
            var response = await _httpClient.GetAsync($"https://api.scryfall.com/cards/named?exact={encodedName}");
            
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                var scryfallCard = JsonSerializer.Deserialize<ScryfallCard>(json, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });
                
                if (scryfallCard != null)
                {
                    var imageData = await DownloadImageAsync(scryfallCard.image_uris?.normal ?? "");
                    
                    return new Card
                    {
                        name = scryfallCard.name,
                        manaCost = scryfallCard.mana_cost ?? "",
                        type = scryfallCard.type_line ?? "",
                        text = scryfallCard.oracle_text ?? "",
                        power = scryfallCard.power ?? "",
                        toughness = scryfallCard.toughness ?? "",
                        imageData = imageData,
                        colors = scryfallCard.colors ?? new List<string>(),
                        rarity = scryfallCard.rarity ?? "common",
                        setCode = scryfallCard.set ?? "",
                        cachedAt = DateTime.UtcNow
                    };
                }
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                // Try fuzzy search if exact match fails
                return await FetchCardWithFuzzySearchAsync(card);
            }
        }
        catch (HttpRequestException ex)
        {
            Console.WriteLine($"Network error fetching card from API: {ex.Message}");
        }
        catch (TaskCanceledException ex)
        {
            Console.WriteLine($"Request timeout fetching card from API: {ex.Message}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error fetching card from API: {ex.Message}");
        }

        return null;
    }
    
    public async Task<Card?> FetchCardWithFuzzySearchAsync(Card card)
    {
        try
        {
            await Task.Delay(50);
            
            var encodedName = Uri.EscapeDataString(card.name);
            var response = await _httpClient.GetAsync($"https://api.scryfall.com/cards/named?fuzzy={encodedName}");
            
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                var scryfallCard = JsonSerializer.Deserialize<ScryfallCard>(json, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });
                
                if (scryfallCard != null)
                {
                    var imageData = await DownloadImageAsync(scryfallCard.image_uris?.normal ?? "");
                    
                    return new Card
                    {
                        name = scryfallCard.name,
                        manaCost = scryfallCard.mana_cost ?? "",
                        type = scryfallCard.type_line ?? "",
                        text = scryfallCard.oracle_text ?? "",
                        power = scryfallCard.power ?? "",
                        toughness = scryfallCard.toughness ?? "",
                        imageData = imageData,
                        colors = scryfallCard.colors ?? new List<string>(),
                        rarity = scryfallCard.rarity ?? "common",
                        setCode = scryfallCard.set ?? "",
                        cachedAt = DateTime.UtcNow
                    };
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in fuzzy search: {ex.Message}");
        }

        return null;
    }

    private async Task<byte[]?> DownloadImageAsync(string imageUrl)
    {
        if (string.IsNullOrEmpty(imageUrl)) return null;

        try
        {
            await Task.Delay(50);
            var response = await _httpClient.GetAsync(imageUrl);
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadAsByteArrayAsync();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error downloading image: {ex.Message}");
        }

        return null;
    }
    
    public Card CreateFallbackCard(Card card)
    {
        return new Card
        {
            name = card.name,
            manaCost = card.manaCost ?? "",
            type = card.type ?? "",
            text = card.text ?? "",
            power = "0",
            toughness = "0",
            colors = new List<string>(),
            rarity = "common",
            setCode = "",
            cachedAt = DateTime.UtcNow
        };
    }
}