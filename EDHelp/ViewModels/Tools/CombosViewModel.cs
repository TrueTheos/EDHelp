using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dock.Model.Mvvm.Controls;
using EDHelp.Models;
using EDHelp.Services;

namespace EDHelp.ViewModels.Tools;

public partial class CombosViewModel : Tool
{
    private readonly IComboFinderService _comboFinderService;
    private readonly DeckBuilderState _state;

    [ObservableProperty]
    private ObservableCollection<Combo> _combos = new();

    [ObservableProperty]
    private bool _isLoading;

    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public object? Context { get; set; }

    public CombosViewModel(IComboFinderService comboFinderService, DeckBuilderState state)
    {
        _comboFinderService = comboFinderService;
        _state = state;

        _state.CardsChanged += OnCardsChanged;
    }

    private async void OnCardsChanged()
    {
        await UpdateCombos();
    }

    [RelayCommand]
    private async Task UpdateCombos()
    {
        IsLoading = true;
        try
        {
            var cards = _state.Cards.Select(dc => dc.card).ToList();
            var foundCombos = await _comboFinderService.FindCombosInDeck(cards);

            _combos.Clear();
            foreach (var combo in foundCombos)
            {
                _combos.Add(combo);
            }
        }
        finally
        {
            IsLoading = false;
        }
    }
}
