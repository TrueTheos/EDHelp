using System.Collections.Generic;
using System.Threading.Tasks;
using EDHelp.Models;

namespace EDHelp.Services;

public interface ICardCacheService
{
    public Task InitializeAsync();
    public Task<Card?> GetCardFromMemoryCache(Card card);
    public Task<Card> GetCard(Card card);
    public Task<List<DeckCard>> FetchDeck(List<DeckCard> deck);
    public List<string> FindBestCardNameMatches(string userInput, int limit = 5);
    public Task<Card?> GetCardByName(string cardName);
}