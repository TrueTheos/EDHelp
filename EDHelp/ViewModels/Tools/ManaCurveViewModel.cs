using System.Collections.Generic;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Dock.Model.Mvvm.Controls;

namespace EDHelp.ViewModels.Tools;

public partial class ManaCurveViewModel : Tool
{
    private readonly DeckBuilderState _state;

    [ObservableProperty]
    private ObservableCollection<ManaCurvePoint> _manaCurve = new();

    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public object? Context { get; set; }

    public ManaCurveViewModel(DeckBuilderState state)
    {
        _state = state;
        _state.CardsChanged += OnCardsChanged;
        
        CalculateManaCurve();
    }

    private void OnCardsChanged()
    {
        CalculateManaCurve();
    }

    private void CalculateManaCurve()
    {
        var curve = new Dictionary<int, int>();

        foreach (var deckCard in _state.Cards)
        {
            var cmc = GetConvertedManaCost(deckCard.card.manaCost);
            if (cmc >= 7) cmc = 7;

            if (curve.ContainsKey(cmc))
                curve[cmc] += deckCard.Quantity;
            else
                curve[cmc] = deckCard.Quantity;
        }

        _manaCurve.Clear();
        for (int i = 0; i <= 7; i++)
        {
            _manaCurve.Add(new ManaCurvePoint
            {
                manaCost = i == 7 ? "7+" : i.ToString(),
                count = curve.ContainsKey(i) ? curve[i] : 0
            });
        }
    }

    private int GetConvertedManaCost(string? manaCost)
    {
        if (string.IsNullOrEmpty(manaCost)) return 0;

        var cmc = 0;
        var i = 0;

        while (i < manaCost.Length)
        {
            if (char.IsDigit(manaCost[i]))
            {
                cmc += int.Parse(manaCost[i].ToString());
            }
            else if (manaCost[i] == '{' && i + 2 < manaCost.Length && manaCost[i + 2] == '}')
            {
                var symbol = manaCost[i + 1];
                if (char.IsDigit(symbol))
                    cmc += int.Parse(symbol.ToString());
                else if (symbol is 'W' or 'U' or 'B' or 'R' or 'G' or 'C')
                    cmc += 1;
                i += 2;
            }
            i++;
        }

        return cmc;
    }
}

public class ManaCurvePoint
{
    public string manaCost { get; set; } = string.Empty;
    public int count { get; set; }
}