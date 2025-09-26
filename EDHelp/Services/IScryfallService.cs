using System.Collections.Generic;
using System.Threading.Tasks;
using EDHelp.Models;

namespace EDHelp.Services;

public interface IScryfallService
{
    public Task<Dictionary<string, Card>> FetchCardBatch(List<Card> cards);
    public Task<Dictionary<string, Card>> FetchCardsBulkFromApi(List<DeckCard> deckCards);
    public Task<Card?> FetchCardAsync(Card card);
    public Task<Card?> FetchCardWithFuzzySearchAsync(Card card);
    public Card CreateFallbackCard(Card card);
    public Task<List<string>> GetAllCardNames();
}