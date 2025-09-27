using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;
using EDHelp.Models;
using FuzzySharp;

namespace EDHelp.Services;

public class CardCacheService : ICardCacheService
{
    private readonly string _cacheDirectory;
    private readonly Dictionary<string, Card> _memoryCache;
    private IScryfallService _scryfallService;
    
    private List<string> _allCardNames = new();
    
    public CardCacheService(IScryfallService scryfallService)
    {
        _scryfallService = scryfallService;
        
        _cacheDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "EDHelp",
            "CardCache"
        );
        _memoryCache = new Dictionary<string, Card>();
        
        Directory.CreateDirectory(_cacheDirectory);

        LoadCacheIndex();
    }
    
    public async Task InitializeAsync()
    {
        _allCardNames = await _scryfallService.GetAllCardNames();
    }

    #region Card Search
    public List<string> FindBestCardNameMatches(string userInput, int limit = 5)
    {
        if (string.IsNullOrWhiteSpace(userInput) || userInput.Length < 2)
            return new List<string>();
            
        var inputLower = userInput.ToLowerInvariant();
        
        var (startIndex, endIndex) = FindPrefixRange(inputLower);
        
        if (startIndex == -1)
            return new List<string>();
        
        var rangeSize = endIndex - startIndex + 1;
        
        if (rangeSize <= limit)
        {
            return _allCardNames.GetRange(startIndex, rangeSize);
        }
        
        var candidates = _allCardNames.GetRange(startIndex, Math.Min(rangeSize, limit * 20));
        
        var matches = Process.ExtractTop(userInput, candidates, limit: limit)
            .Select(result => result.Value)
            .ToList();
            
        return matches;
    }

    private (int start, int end) FindPrefixRange(string prefix)
    {
        int start = FindFirstPrefixMatch(prefix);
        if (start == -1)
            return (-1, -1);
            
        int end = FindLastPrefixMatch(prefix);
        return (start, end);
    }

    private int FindFirstPrefixMatch(string prefix)
    {
        int left = 0, right = _allCardNames.Count - 1;
        int result = -1;
        
        while (left <= right)
        {
            int mid = left + (right - left) / 2;
            var midName = _allCardNames[mid].ToLowerInvariant();
            
            if (midName.StartsWith(prefix))
            {
                result = mid;
                right = mid - 1;
            }
            else if (string.Compare(midName, prefix, StringComparison.Ordinal) < 0)
            {
                left = mid + 1;
            }
            else
            {
                right = mid - 1;
            }
        }
        
        return result;
    }

    private int FindLastPrefixMatch(string prefix)
    {
        int left = 0, right = _allCardNames.Count - 1;
        int result = -1;
        
        while (left <= right)
        {
            int mid = left + (right - left) / 2;
            var midName = _allCardNames[mid].ToLowerInvariant();
            
            if (midName.StartsWith(prefix))
            {
                result = mid;
                left = mid + 1;
            }
            else if (string.Compare(midName, prefix, StringComparison.Ordinal) < 0)
            {
                left = mid + 1;
            }
            else
            {
                right = mid - 1;
            }
        }
        
        return result;
    }
    
    #endregion
    
    public async Task<Card?> GetCardByName(string cardName)
    {
        if (string.IsNullOrWhiteSpace(cardName))
            return null;

        var card = new Card { name = cardName };
            
        return await GetCard(card);
    }
    
    public async Task<Card?> GetCardFromMemoryCache(Card card)
    {
        var cacheKey = GenerateCacheKey(card);
        
        if (_memoryCache.ContainsKey(cacheKey))
        {
            var cached = _memoryCache[cacheKey];
            if (cached.cachedAt > DateTime.UtcNow.AddHours(-24))
            {
                return cached;
            }
            else
            {
                _memoryCache.Remove(cacheKey);
            }
        }

        var cacheFilePath = Path.Combine(_cacheDirectory, $"{cacheKey}.json");
        if (File.Exists(cacheFilePath))
        {
            try
            {
                var fileInfo = new FileInfo(cacheFilePath);
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
                    File.Delete(cacheFilePath);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading cached card: {ex.Message}");
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

        Card = await _scryfallService.FetchCardAsync(card);
        
        if (Card != null)
        {
            await SaveToCacheAsync(card, Card);
            _memoryCache[GenerateCacheKey(card)] = Card;
        }

        return Card ?? _scryfallService.CreateFallbackCard(card);
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
            var bulkResults = await _scryfallService.FetchCardsBulkFromApi(unCards);

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
            var cacheFiles = Directory.GetFiles(_cacheDirectory, "*.json");
            var recentFiles = cacheFiles
                .Select(f => new FileInfo(f))
                .Where(f => f.LastWriteTime > DateTime.Now.AddHours(-24)) 
                .OrderByDescending(f => f.LastWriteTime)
                .Take(50);

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
                    try { file.Delete(); } catch { }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading cache index: {ex.Message}");
        }
    }

    private string GenerateCacheKey(Card card)
    {
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