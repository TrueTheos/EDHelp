using System;
using Dock.Model.Controls;
using Dock.Model.Core;
using Dock.Model.Mvvm;
using Dock.Model.Mvvm.Controls;
using EDHelp.Models;
using System.Collections.Generic;
using EDHelp.ViewModels.Documents;
using EDHelp.ViewModels.Tools;
using Microsoft.Extensions.DependencyInjection;

namespace EDHelp.ViewModels;

public class DockFactory : Factory
{
    private readonly IServiceProvider _provider;

    public DockFactory(IServiceProvider provider)
    {
        _provider = provider;
    }

    public override IRootDock CreateLayout()
    {
        // Documents
        var deckList = _provider.GetRequiredService<DeckListViewModel>();
        deckList.Id = "DeckList";
        deckList.Title = "Deck List";

        var cardPreview = _provider.GetRequiredService<CardPreviewViewModel>();
        cardPreview.Id = "CardPreview";
        cardPreview.Title = "Card Preview";

        // Tools
        var popularDecks = _provider.GetRequiredService<PopularDecksViewModel>();
        popularDecks.Id = "PopularDecks";
        popularDecks.Title = "Popular Decks";

        var commonCards = _provider.GetRequiredService<CommonCardsViewModel>();
        commonCards.Id = "CommonCards";
        commonCards.Title = "Common Cards";

        var manaCurve = _provider.GetRequiredService<ManaCurveViewModel>();
        manaCurve.Id = "ManaCurve";
        manaCurve.Title = "Mana Curve";

        var combos = _provider.GetRequiredService<CombosViewModel>();
        combos.Id = "Combos";
        combos.Title = "Combos";

        // Create left document dock with deck list and card preview
        var leftDocumentDock = new DocumentDock
        {
            Id = "LeftDocuments",
            Title = "Documents",
            VisibleDockables = CreateList<IDockable>(deckList, cardPreview),
            ActiveDockable = deckList,
            CanCreateDocument = false
        };

        // Create right tool dock with all tools
        var rightToolDock = new ToolDock
        {
            Id = "RightTools",
            Title = "Tools",
            VisibleDockables = CreateList<IDockable>(popularDecks, commonCards, manaCurve, combos),
            ActiveDockable = popularDecks,
            Alignment = Alignment.Right,
            GripMode = GripMode.Visible
        };

        // Main proportional dock layout
        var proportionalDock = CreateProportionalDock();
        proportionalDock.Id = "MainLayout";
        proportionalDock.Title = "MainLayout";
        proportionalDock.Orientation = Orientation.Horizontal;
        proportionalDock.VisibleDockables = CreateList<IDockable>(
            leftDocumentDock,
            CreateProportionalDockSplitter(),
            rightToolDock
        );

        // Root dock
        var root = CreateRootDock();
        root.Id = "Root";
        root.Title = "Root";
        root.VisibleDockables = CreateList<IDockable>(proportionalDock);
        root.ActiveDockable = proportionalDock;
        root.DefaultDockable = proportionalDock;

        root.LeftPinnedDockables = CreateList<IDockable>();
        root.RightPinnedDockables = CreateList<IDockable>();
        root.TopPinnedDockables = CreateList<IDockable>();
        root.BottomPinnedDockables = CreateList<IDockable>();

        return root;
    }

    public override void InitLayout(IDockable layout)
    {
        DockableLocator = new Dictionary<string, Func<IDockable?>>
        {
            ["DeckList"] = () => _provider.GetRequiredService<DeckListViewModel>(),
            ["CardPreview"] = () => _provider.GetRequiredService<CardPreviewViewModel>(),
            ["PopularDecks"] = () => _provider.GetRequiredService<PopularDecksViewModel>(),
            ["CommonCards"] = () => _provider.GetRequiredService<CommonCardsViewModel>(),
            ["ManaCurve"] = () => _provider.GetRequiredService<ManaCurveViewModel>(),
            ["Combos"] = () => _provider.GetRequiredService<CombosViewModel>()
        };

        ContextLocator = new Dictionary<string, Func<object?>>
        {
            ["DeckList"] = () => _provider.GetRequiredService<DemoData>(),
            ["CardPreview"] = () => _provider.GetRequiredService<DemoData>(),
            ["PopularDecks"] = () => _provider.GetRequiredService<DemoData>(),
            ["CommonCards"] = () => _provider.GetRequiredService<DemoData>(),
            ["ManaCurve"] = () => _provider.GetRequiredService<DemoData>(),
            ["Combos"] = () => _provider.GetRequiredService<DemoData>()
        };

        DefaultContextLocator = () => _provider.GetService(typeof(DemoData));

        base.InitLayout(layout);
    }
}