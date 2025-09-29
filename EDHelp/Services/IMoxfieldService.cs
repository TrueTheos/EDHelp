using System.Collections.Generic;
using System.Threading.Tasks;
using EDHelp.Models;

namespace EDHelp.Services;

public interface IMoxfieldService
{
    public Task<List<MoxfieldDeck>> ExportTopDecksForCommander(string cardName, int topCount = 5);
}