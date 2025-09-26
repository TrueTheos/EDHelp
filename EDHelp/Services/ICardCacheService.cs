using System.Collections.Generic;
using System.Threading.Tasks;
using EDHelp.Models;

namespace EDHelp.Services;

public interface ICardCacheService
{
    public Task<Card?> GetCardFromMemoryCache(Card card);
    public Task<Card> GetCard(Card card);
    public Task<List<DeckCard>> FetchDeck(List<DeckCard> deck);
    public string FindBestCardNameMatch(string userInput);
    public List<string> FindBestCardNameMatches(string userInput, int limit = 5);
}