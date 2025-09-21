using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;
using EDHelp.Models;

namespace EDHelp.Services;

public class CardCacheService
{
    private HttpClient _httpClient = new();
    private readonly string _cacheDirectory;
    private readonly Dictionary<string, Card> _memoryCache;

    public CardCacheService()
    {
        _cacheDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "EDHelp",
            "CardCache"
        );
        _memoryCache = new Dictionary<string, Card>();
        
        Directory.CreateDirectory(_cacheDirectory);
        
        // Set required headers for Scryfall API
        SetupHttpClientHeaders();
        
        LoadCacheIndex();
    }

    private void SetupHttpClientHeaders()
    {
        // Add required User-Agent header - replace with your actual app name/version
        if (!_httpClient.DefaultRequestHeaders.Contains("User-Agent"))
        {
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "EDHelp/1.0");
        }
        
        // Add required Accept header
        if (!_httpClient.DefaultRequestHeaders.Contains("Accept"))
        {
            _httpClient.DefaultRequestHeaders.Add("Accept", "application/json;q=0.9,*/*;q=0.8");
        }
    }

    public async Task<Card?> GetCardFromMemoryCache(Card card)
    {
        var cacheKey = GenerateCacheKey(card);
        
        // Check memory cache first
        if (_memoryCache.ContainsKey(cacheKey))
        {
            var cached = _memoryCache[cacheKey];
            // Check if cache is still fresh (e.g., within 24 hours)
            if (cached.cachedAt > DateTime.UtcNow.AddHours(-24))
            {
                return cached;
            }
            else
            {
                // Remove stale cache entry
                _memoryCache.Remove(cacheKey);
            }
        }

        // Check disk cache
        var cacheFilePath = Path.Combine(_cacheDirectory, $"{cacheKey}.json");
        if (File.Exists(cacheFilePath))
        {
            try
            {
                var fileInfo = new FileInfo(cacheFilePath);
                // Check if file is still fresh
                if (fileInfo.LastWriteTime > DateTime.Now.AddHours(-24))
                {
                    var json = await File.ReadAllTextAsync(cacheFilePath);
                    var Card = JsonSerializer.Deserialize<Card>(json);
                    if (Card != null)
                    {
                        _memoryCache[cacheKey] = Card;
                        return Card;
                    }
                }
                else
                {
                    // Delete stale cache file
                    File.Delete(cacheFilePath);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading cached card: {ex.Message}");
                // Try to delete corrupted file
                try { File.Delete(cacheFilePath); } catch { }
            }
        }

        return null;
    }

    public async Task<Card> GetCard(Card card)
    {
        var Card = await GetCardFromMemoryCache(card);
        if (Card != null)
        {
            return Card;
        }

        Card = await FetchCardFromApiAsync(card);
        
        if (Card != null)
        {
            await SaveToCacheAsync(card, Card);
            _memoryCache[GenerateCacheKey(card)] = Card;
        }

        return Card ?? CreateFallbackCard(card);
    }

    public async Task<List<DeckCard>> FetchDeck(List<DeckCard> deck)
    {
        var unCards = new List<DeckCard>();

        foreach (var deckCard in deck)
        {
            if (deckCard == null) continue;

            var cached = await GetCardFromMemoryCache(deckCard.card);
            if (cached != null)
            {
                deckCard.card = cached;
            }
            else
            {
                unCards.Add(deckCard);
            }
        }

        if (unCards.Count > 0)
        {
            var bulkResults = await FetchCardsBulkFromApi(unCards);

            foreach (var kvp in bulkResults)
            {
                var matchingDeckCards = unCards.Where(c => c.card.name == kvp.Key);
                foreach (var deckCard in matchingDeckCards)
                {
                    deckCard.card = kvp.Value;

                    await SaveToCacheAsync(deckCard.card, kvp.Value);
                    _memoryCache[GenerateCacheKey(deckCard.card)] = kvp.Value;
                }
            }
        }

        return deck;
    }
    
    private async Task<Dictionary<string, Card>> FetchCardsBulkFromApi(List<DeckCard> deckCards)
    {
        var result = new Dictionary<string, Card>();
    
        // Scryfall's collection endpoint has a limit of 75 cards per request
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
            
                // Rate limiting between batches
                await Task.Delay(100);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error fetching batch: {ex.Message}");
            
                // Fall back to individual requests for this batch
                foreach (var card in batch)
                {
                    try
                    {
                        await Task.Delay(75); // Individual request rate limiting
                        var individualResult = await FetchCardFromApiAsync(card);
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
    
    private async Task<Dictionary<string, Card>> FetchCardBatch(List<Card> cards)
{
    var result = new Dictionary<string, Card>();
    
    // Prepare bulk request payload
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
            // Process successful results
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
            
            // Handle not found cards with fallback
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
    }
    else
    {
        throw new HttpRequestException($"Bulk request failed with status: {response.StatusCode}");
    }
    
    return result;
}

    
    private async Task<Card?> FetchCardFromApiAsync(Card card)
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

    private async Task<Card?> FetchCardWithFuzzySearchAsync(Card card)
    {
        try
        {
            await Task.Delay(50); // Rate limiting
            
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
            await Task.Delay(50); // Rate limiting for image downloads
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

    private async Task SaveToCacheAsync(Card card, Card Card)
    {
        try
        {
            var cacheKey = GenerateCacheKey(card);
            var cacheFilePath = Path.Combine(_cacheDirectory, $"{cacheKey}.json");
            var json = JsonSerializer.Serialize(Card, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(cacheFilePath, json);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error saving card to cache: {ex.Message}");
        }
    }

    private void LoadCacheIndex()
    {
        try
        {
            // Load recently used cards into memory cache
            var cacheFiles = Directory.GetFiles(_cacheDirectory, "*.json");
            var recentFiles = cacheFiles
                .Select(f => new FileInfo(f))
                .Where(f => f.LastWriteTime > DateTime.Now.AddHours(-24)) // Only load fresh cache
                .OrderByDescending(f => f.LastWriteTime)
                .Take(50); // Keep 50 most recent in memory

            foreach (var file in recentFiles)
            {
                try
                {
                    var json = File.ReadAllText(file.FullName);
                    var Card = JsonSerializer.Deserialize<Card>(json);
                    if (Card != null)
                    {
                        var cacheKey = Path.GetFileNameWithoutExtension(file.Name);
                        _memoryCache[cacheKey] = Card;
                    }
                }
                catch
                {
                    // Delete corrupted cache files
                    try { file.Delete(); } catch { }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading cache index: {ex.Message}");
        }
    }

    private Card CreateFallbackCard(Card card)
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

    private string GenerateCacheKey(Card card)
    {
        // More robust cache key generation
        return card.name
            .Replace(" ", "_")
            .Replace("'", "")
            .Replace(",", "")
            .Replace("\"", "")
            .Replace("/", "_")
            .Replace("\\", "_")
            .ToLowerInvariant();
    }

    public void ClearCache()
    {
        try
        {
            Directory.Delete(_cacheDirectory, true);
            Directory.CreateDirectory(_cacheDirectory);
            _memoryCache.Clear();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error clearing cache: {ex.Message}");
        }
    }

    public void ClearStaleCache()
    {
        try
        {
            var staleFiles = Directory.GetFiles(_cacheDirectory, "*.json")
                .Select(f => new FileInfo(f))
                .Where(f => f.LastWriteTime < DateTime.Now.AddHours(-24))
                .ToList();

            foreach (var file in staleFiles)
            {
                file.Delete();
            }

            // Clear stale entries from memory cache
            var staleKeys = _memoryCache
                .Where(kvp => kvp.Value.cachedAt < DateTime.UtcNow.AddHours(-24))
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var key in staleKeys)
            {
                _memoryCache.Remove(key);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error clearing stale cache: {ex.Message}");
        }
    }
}