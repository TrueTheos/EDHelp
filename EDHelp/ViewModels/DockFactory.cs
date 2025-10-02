using System;
using System.Collections.Generic;
using System.Linq;
using Dock.Avalonia.Controls;
using Dock.Model.Controls;
using Dock.Model.Core;
using Dock.Model.Mvvm;
using Dock.Model.Mvvm.Controls;
using EDHelp.Models;
using EDHelp.Models.Documents;
using EDHelp.Models.Tools;
using EDHelp.ViewModels.Docks;
using EDHelp.ViewModels.Documents;
using EDHelp.ViewModels.Tools;
using Microsoft.Extensions.DependencyInjection;

namespace EDHelp.ViewModels;

public class DockFactory : Factory
{
    private IRootDock? _rootDock;
    private IDocumentDock? _documentDock;
    private IServiceProvider _serviceProvider;
    
    public DockFactory( IServiceProvider provider)
    {
        _serviceProvider = provider;
    }
    
    public override IDocumentDock CreateDocumentDock() => new CustomDocumentDock();

    public override IRootDock CreateLayout()
    {
        var document1 = new DocumentViewModel {Id = "Document1", Title = "Document1"};
        
        var deckBuilderView = _serviceProvider.GetRequiredService<DeckBuilderViewModel>();
        deckBuilderView.Id = "DeckBuilder";
        deckBuilderView.Title = "DeckBuilder";

        var cardInfoTool = _serviceProvider.GetRequiredService<CardInfoToolViewModel>();
        cardInfoTool.Id = "CardInfoTool";
        cardInfoTool.Title = "Card Info";

        var toolsList = new List<IDockable> { cardInfoTool };

        var rightDock = new ToolDock()
        {
            Proportion = 0.25,
            ActiveDockable = toolsList.FirstOrDefault(),
            VisibleDockables = CreateList<IDockable>(toolsList.ToArray()),
        };
        
        var documentDock = new CustomDocumentDock
        {
            // DockGroup = "CustomDocumentDock",
            IsCollapsable = false,
            ActiveDockable = document1,
            VisibleDockables = CreateList<IDockable>(document1),
            CanCreateDocument = true,
            // CanDrop = false,
            EnableWindowDrag = true,
            // CanCloseLastDockable = false,
        };
        
        var mainLayout = new ProportionalDock
        {
            // EnableGlobalDocking = false,
            Orientation = Orientation.Horizontal,
            VisibleDockables = CreateList<IDockable>
            (
                rightDock,
                new ProportionalDockSplitter { ResizePreview = true },
                documentDock
            )
        };

        deckBuilderView.ActiveDockable = mainLayout;
        deckBuilderView.VisibleDockables = CreateList<IDockable>(mainLayout);

        var rootDock = CreateRootDock();

        rootDock.IsCollapsable = false;
        rootDock.DefaultDockable = deckBuilderView;
        rootDock.VisibleDockables = CreateList<IDockable>(deckBuilderView);

        rootDock.LeftPinnedDockables = CreateList<IDockable>();
        rootDock.RightPinnedDockables = CreateList<IDockable>();
        rootDock.TopPinnedDockables = CreateList<IDockable>();
        rootDock.BottomPinnedDockables = CreateList<IDockable>();

        rootDock.PinnedDock = null;

        _documentDock = documentDock;
        _rootDock = rootDock;
            
        return rootDock;
    }
    
    public override IDockWindow? CreateWindowFrom(IDockable dockable)
    {
        var window = base.CreateWindowFrom(dockable);

        if (window != null)
        {
            window.Title = "EDHelp";
        }
        return window;
    }

    public override void InitLayout(IDockable layout)
    {
        var contextLocator = new Dictionary<string, Func<object?>>
        {
            ["Document1"] = () => new DemoDocument(),
            ["CardInfoTool"] = () => new CardInfoToolModel(),
            ["DeckBuilder"] = () => _serviceProvider.GetRequiredService<DeckBuilderViewModel>()
        };
        
        ContextLocator = contextLocator;

        var dockableLocator = new Dictionary<string, Func<IDockable?>>()
        {
            ["Root"] = () => _rootDock,
            ["Documents"] = () => _documentDock
        };
        
        DockableLocator = dockableLocator;

        HostWindowLocator = new Dictionary<string, Func<IHostWindow?>>
        {
            [nameof(IDockWindow)] = () => new HostWindow()
        };

        base.InitLayout(layout);
    }
}