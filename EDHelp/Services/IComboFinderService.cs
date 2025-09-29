using System.Collections.Generic;
using System.Threading.Tasks;
using EDHelp.Models;

namespace EDHelp.Services;

public interface IComboFinderService
{
    public Task<List<Combo>> FindCombosInDeck(List<Card> cards);
}