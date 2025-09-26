using System.Collections.Generic;
using System.Threading.Tasks;

namespace EDHelp.Services;

public interface IMoxfieldService
{
    public Task<List<List<string>>> ExportTopDecksForCommander(string cardName, int topCount = 5);
}