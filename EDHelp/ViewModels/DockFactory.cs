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
    private readonly object _context;
    private IRootDock? _rootDock;
    private IDocumentDock? _documentDock;
    private IServiceProvider _serviceProvider;
    
    public DockFactory(object context, IServiceProvider provider)
    {
        _serviceProvider = provider;
        _context = context;
    }
    
    public override IDocumentDock CreateDocumentDock() => new CustomDocumentDock();

    public override IRootDock CreateLayout()
    {
        var deckBuilderView = _serviceProvider.GetRequiredService<DeckBuilderViewModel>();
        deckBuilderView.Id = "DeckBuilder";
        deckBuilderView.Title = "DeckBuilder";

        var cardInfoTool = _serviceProvider.GetRequiredService<CardInfoToolViewModel>();
        cardInfoTool.Id = "CardInfoTool";
        cardInfoTool.Title = "Card Info";

        var rootDock = CreateRootDock();

        rootDock.VisibleDockables = CreateList<IDockable>(deckBuilderView);
        rootDock.ActiveDockable = deckBuilderView;
        rootDock.DefaultDockable = deckBuilderView;
            
        return rootDock;
    }

    public override void InitLayout(IDockable layout)
    {
        DockableLocator = new Dictionary<string, Func<IDockable?>>
        {
            ["DeckBuilder"] = () =>
            {
                var vm = _serviceProvider.GetRequiredService<DeckBuilderViewModel>();
                vm.Id = "DeckBuilder";
                vm.Title = "DeckBuilder";
                return vm;
            },
            ["CardInfoTool"] = () =>
            {
                var vm = _serviceProvider.GetRequiredService<CardInfoToolViewModel>();
                vm.Id = "CardInfoTool";
                vm.Title = "Card Info";
                return vm;
            }
        };

        ContextLocator = new Dictionary<string, Func<object?>>
        {
            ["DeckBuilder"] = () => _serviceProvider.GetService(typeof(DemoData)),
            ["CardInfoTool"] = () => _serviceProvider.GetService(typeof(DemoData))
        };

        DefaultContextLocator = () => _serviceProvider.GetService(typeof(DemoData));

        base.InitLayout(layout);
    }
}