using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dock.Model.Mvvm.Controls;
using EDHelp.Models;
using EDHelp.Services;

namespace EDHelp.ViewModels.Tools;

public partial class PopularDecksViewModel : Tool
{
    private readonly IMoxfieldService _moxfieldService;
    private readonly DeckBuilderState _state;

    [ObservableProperty]
    private ObservableCollection<MoxfieldDeck> _moxfieldBestDecks = new();

    [ObservableProperty]
    private bool _isLoading;

    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public object? Context { get; set; }

    public PopularDecksViewModel(IMoxfieldService moxfieldService, DeckBuilderState state)
    {
        _moxfieldService = moxfieldService;
        _state = state;

        _state.CommanderChanged += OnCommanderChanged;
    }

    private async void OnCommanderChanged()
    {
        if (_state.Commander != null)
        {
            await LoadPopularDecks(_state.Commander.name);
        }
    }

    public async Task LoadPopularDecks(string commanderName)
    {
        IsLoading = true;
        try
        {
            var decks = await _moxfieldService.ExportTopDecksForCommander(commanderName);

            _moxfieldBestDecks.Clear();
            foreach (var deck in decks)
            {
                _moxfieldBestDecks.Add(deck);
            }
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void OpenMoxfieldDeck(string link)
    {
        Process.Start(new ProcessStartInfo(link) { UseShellExecute = true });
    }
}