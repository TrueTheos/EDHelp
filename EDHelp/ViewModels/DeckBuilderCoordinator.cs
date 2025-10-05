using System;
using System.Threading.Tasks;
using EDHelp.Models;

namespace EDHelp.ViewModels;

public class DeckBuilderCoordinator
{
    private readonly IServiceProvider _provider;
    private readonly DeckBuilderState _state;

    public DeckBuilderCoordinator(IServiceProvider provider, DeckBuilderState state)
    {
        _provider = provider;
        _state = state;
    }

    public async Task InitializeAsync(Deck deck)
    {
        _state.Commander = deck.commander;
        _state.TotalCards = deck.totalCards;
    
        if (_state.Commander != null)
        {
            _state.NotifyCommanderChanged();
        }
    }
}