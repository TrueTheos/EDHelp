using System;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using EDHelp.ViewModels;
using EDHelp.Models;
using EDHelp.ViewModels.Tools;
using EDHelp.Views;
using Microsoft.Extensions.DependencyInjection;

namespace EDHelp;

public partial class App : Application
{
    public IServiceProvider? ServiceProvider { get; }
    private readonly ViewLocator _viewLocator;

    public App()
    {
    }

    public App(IServiceProvider? serviceProvider, ViewLocator viewLocator)
    {
        ServiceProvider = serviceProvider;
        _viewLocator = viewLocator;
    }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
        DataTemplates.Insert(0, _viewLocator);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop && ServiceProvider != null)
        {
            var vm = ServiceProvider.GetRequiredService<MainWindowViewModel>();
            var view = ServiceProvider.GetRequiredService<MainWindow>();
            view.DataContext = vm;
            
            view.Closing += async (_, _) => await vm.SaveLayoutAsync();
            desktop.MainWindow = view;
            desktop.Exit += async (_, _) => await vm.SaveLayoutAsync();

            base.OnFrameworkInitializationCompleted();
        }
    }
}